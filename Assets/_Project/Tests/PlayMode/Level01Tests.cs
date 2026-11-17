using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Poeirinha.Tests
{
    /// <summary>
    /// Testes automáticos da fase (Window > General > Test Runner > PlayMode).
    /// Cobrem boa parte do checklist de QA do GDD (seção 17). Eles NÃO substituem
    /// jogar a build, mas pegam regressões rápido.
    /// </summary>
    public class Level01Tests
    {
        const int Wall = 7, Occluder = 8;

        GameManager gm;
        RobotController robot;
        BatterySystem battery;
        CliffSensor cliff;
        CleaningManager cleaning;
        LevelTimer timer;
        CatAI cat;
        Camera cam;

        // Um ponto aberto da sala, longe de móveis, do gato e da base.
        static readonly Vector3 OpenSpot = new Vector3(5.6f, 0.005f, 1.2f);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene(SceneLoader.Level01);
            yield return null;
            yield return null;
            Bind();
        }

        void Bind()
        {
            gm = GameManager.Instance;
            robot = Object.FindFirstObjectByType<RobotController>();
            battery = robot.GetComponent<BatterySystem>();
            cliff = robot.GetComponent<CliffSensor>();
            cleaning = Object.FindFirstObjectByType<CleaningManager>();
            timer = Object.FindFirstObjectByType<LevelTimer>();
            cat = Object.FindFirstObjectByType<CatAI>();
            cam = Camera.main;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        /// <summary>Tira o gato do caminho para não interferir nos testes de física.</summary>
        void ParkCat()
        {
            cat.gameObject.SetActive(false);
        }

        [Test]
        public void T01_SceneStartsPlaying()
        {
            Assert.IsNotNull(gm);
            Assert.AreEqual(GameState.Playing, gm.State);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.That(cleaning.Total, Is.InRange(55, 65), "~60 manchas");
            int under = cleaning.Spots.Count(s => s.UnderFurniture);
            Assert.That(under, Is.GreaterThanOrEqualTo(12), "~15 manchas embaixo de móveis");
            Assert.AreEqual(100f, battery.Charge, 0.5f);
            Assert.AreEqual(180f, timer.Remaining, 0.5f);
        }

        [UnityTest]
        public IEnumerator T02_RobotDrivesTurnsAndStaysFlat()
        {
            ParkCat();
            robot.Teleport(OpenSpot, 90f);
            yield return new WaitForFixedUpdate();
            Vector3 start = robot.transform.position;

            robot.SetExternalInput(1f, 0f);
            yield return new WaitForSeconds(1.0f);
            float moved = robot.transform.position.x - start.x;
            Assert.That(moved, Is.GreaterThan(1.2f), "anda ~2 m/s com aceleração suave");
            Assert.That(moved, Is.LessThan(2.1f), "aceleração não é instantânea");

            robot.SetExternalInput(0f, 1f);
            float yaw0 = robot.transform.eulerAngles.y;
            yield return new WaitForSeconds(0.5f);
            float turned = Mathf.Abs(Mathf.DeltaAngle(yaw0, robot.transform.eulerAngles.y));
            Assert.That(turned, Is.InRange(50f, 100f), "gira ~180°/s");

            robot.ClearExternalInput();
            yield return new WaitForSeconds(0.5f);
            Vector3 e = robot.transform.eulerAngles;
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(0, e.x)) + Mathf.Abs(Mathf.DeltaAngle(0, e.z)), Is.LessThan(0.5f), "não tomba");
            Assert.That(robot.transform.position.y, Is.InRange(-0.01f, 0.03f), "fica no chão");
        }

        [UnityTest]
        public IEnumerator T03_RobotDoesNotPassThroughWallsInTurbo()
        {
            ParkCat();
            // Sala, de frente para a parede oeste, com turbo.
            robot.Teleport(new Vector3(1.2f, 0.005f, 3.95f), -90f);
            robot.SetExternalInput(1f, 0f, true);
            yield return new WaitForSeconds(1.5f);
            Assert.That(robot.transform.position.x, Is.GreaterThan(0.1f), "parede oeste em x = 0");
            // Desliza na quina sem prender: andando em diagonal na parede ele continua se movendo.
            robot.Teleport(new Vector3(6.5f, 0.005f, 3.9f), 30f);
            robot.SetExternalInput(1f, 0f, true);
            yield return new WaitForSeconds(0.6f);
            Vector3 a = robot.transform.position;
            yield return new WaitForSeconds(0.4f);
            Assert.That((robot.transform.position - a).magnitude, Is.GreaterThan(0.2f), "desliza pela parede/batente em vez de prender");
            robot.ClearExternalInput();
        }

        [UnityTest]
        public IEnumerator T04_BatteryDrainsAndCharges()
        {
            ParkCat();
            robot.Teleport(OpenSpot, 0f);
            yield return new WaitForSeconds(1f);
            Assert.That(battery.IsCharging, Is.False);
            Assert.That(battery.Charge, Is.LessThan(100f));

            battery.SetCharge(50f);
            robot.Teleport(new Vector3(4.6f, 0.005f, 4.05f), 90f); // em cima da base
            yield return new WaitForSeconds(1f);
            Assert.That(battery.IsCharging, Is.True);
            Assert.That(battery.Charge, Is.GreaterThan(65f), "+25/s na base");
        }

        [UnityTest]
        public IEnumerator T05_BatteryEmptyIsDefeatAndBlocksVictory()
        {
            ParkCat();
            robot.Teleport(OpenSpot, 0f);
            yield return new WaitForFixedUpdate();
            battery.SetCharge(0.3f);
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(GameState.Defeat, gm.State);
            Assert.AreEqual(DefeatReason.BatteryEmpty, gm.LastDefeatReason);
            gm.Win(); // não pode virar vitória depois
            gm.Lose(DefeatReason.TimeUp); // nem trocar o motivo
            Assert.AreEqual(GameState.Defeat, gm.State);
            Assert.AreEqual(DefeatReason.BatteryEmpty, gm.LastDefeatReason);
        }

        [UnityTest]
        public IEnumerator T06_CollectingDirtWins()
        {
            ParkCat();
            foreach (DirtSpot spot in cleaning.Spots.OrderBy(s => s.UnderFurniture ? 0 : 1))
            {
                if (gm.State != GameState.Playing) break;
                battery.SetCharge(100f);
                Vector3 p = spot.transform.position;
                robot.Teleport(new Vector3(p.x, 0.005f, p.z), 0f);
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                Assert.IsTrue(spot.IsCollected, $"mancha {spot.name} não foi coletada pelo trigger");
            }
            Assert.AreEqual(GameState.Victory, gm.State);
            Assert.That(cleaning.Fraction, Is.GreaterThanOrEqualTo(0.9f));
            Assert.That(gm.CalculateStars(), Is.EqualTo(3));
        }

        [Test]
        public void T07_EveryDirtSpotHasRoomForTheRobot()
        {
            int solid = ~((1 << 2) | (1 << 6) | (1 << 9) | (1 << 5));
            foreach (DirtSpot spot in cleaning.Spots)
            {
                Vector3 p = spot.transform.position;
                bool blocked = Physics.CheckBox(new Vector3(p.x, 0.06f, p.z), new Vector3(0.18f, 0.035f, 0.18f), Quaternion.identity, solid, QueryTriggerInteraction.Ignore);
                Assert.IsFalse(blocked, $"{spot.name} em {p} não cabe o robô");
            }
        }

        [UnityTest]
        public IEnumerator T08_FallingDownStairsIsDefeatAndSensorWarns()
        {
            ParkCat();
            robot.Teleport(new Vector3(7.0f, 0.005f, 0.8f), 90f); // a borda da escada está em x = 7.8
            bool warned = false;
            robot.SetExternalInput(0.6f, 0f);
            for (int i = 0; i < 150 && gm.State == GameState.Playing; i++)
            {
                warned |= cliff.EdgeDetected;
                yield return new WaitForFixedUpdate();
            }
            Assert.IsTrue(warned, "sensor de borda avisou antes da queda");
            Assert.AreEqual(GameState.Defeat, gm.State);
            Assert.AreEqual(DefeatReason.FellDownStairs, gm.LastDefeatReason);
        }

        [UnityTest]
        public IEnumerator T09_TimeUpIsDefeat()
        {
            timer.SetRemaining(0.1f);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(GameState.Defeat, gm.State);
            Assert.AreEqual(DefeatReason.TimeUp, gm.LastDefeatReason);
        }

        [UnityTest]
        public IEnumerator T10_PauseFreezesAndResumes()
        {
            gm.TogglePause();
            Assert.AreEqual(GameState.Paused, gm.State);
            Assert.AreEqual(0f, Time.timeScale);
            float t = timer.Remaining;
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(t, timer.Remaining, 0.0001f, "timer parado na pausa");
            gm.TogglePause();
            Assert.AreEqual(GameState.Playing, gm.State);
            Assert.AreEqual(1f, Time.timeScale);
            yield return new WaitForSeconds(0.2f);
            Assert.That(timer.Remaining, Is.LessThan(t));
        }

        [UnityTest]
        public IEnumerator T11_CatHitPushesAndDrainsOnce()
        {
            // Encosta o robô de frente no gato.
            Vector3 c = cat.transform.position;
            Vector3 dir = cat.transform.forward;
            robot.Teleport(new Vector3(c.x, 0.005f, c.z) + dir * 0.38f, Quaternion.LookRotation(-dir).eulerAngles.y);
            Vector3 before = robot.transform.position;
            float charge = battery.Charge;
            robot.SetExternalInput(0.4f, 0f);
            yield return new WaitForSeconds(0.5f);
            robot.ClearExternalInput();
            float lost = charge - battery.Charge;
            Assert.That(lost, Is.InRange(9.5f, 12f), "−10 uma vez só (invulnerabilidade de 1 s)");
            Vector3 catPos = cat.transform.position;
            Assert.That(Vector3.Distance(robot.transform.position, catPos), Is.GreaterThan(0.3f), "empurrado para longe");
        }

        [UnityTest]
        public IEnumerator T12_CameraNeverInsideWallsAndSeesRobot()
        {
            ParkCat();
            (Vector3 pos, float yaw)[] cases =
            {
                (new Vector3(0.3f, 0.005f, 0.3f), 45f),     // canto sudoeste, câmera iria para dentro do canto
                (new Vector3(3.3f, 0.005f, 4.2f), 180f),   // costas para a parede do quarto
                (new Vector3(6.9f, 0.005f, 4.2f), 180f),   // no vão da cozinha
                (new Vector3(9.0f, 0.005f, 6.9f), 180f),   // de costas para a geladeira/bancada
                (new Vector3(0.9f, 0.005f, 5.0f), 120f),   // perto do guarda-roupa
                (new Vector3(9.7f, 0.005f, 2.6f), -90f),   // parede leste
            };
            var follow = cam.GetComponent<CameraFollow>();
            foreach (var (pos, yaw) in cases)
            {
                robot.Teleport(pos, yaw);
                follow.SnapToTarget();
                yield return new WaitForSeconds(0.4f);
                Vector3 cp = cam.transform.position;
                Assert.IsFalse(Physics.CheckSphere(cp, 0.05f, 1 << Wall, QueryTriggerInteraction.Ignore), $"câmera dentro da parede em {pos}");
                Assert.IsFalse(Physics.Linecast(cp, robot.transform.position + Vector3.up * 0.05f, 1 << Wall, QueryTriggerInteraction.Ignore), $"parede entre câmera e robô em {pos}");
            }
        }

        [UnityTest]
        public IEnumerator T13_SofaFadesWhenRobotIsUnderIt()
        {
            ParkCat();
            FadeableObject sofa = Object.FindObjectsByType<FadeableObject>(FindObjectsSortMode.None).First(f => f.name == "Sofa");
            // Robô na frente do sofá, de frente para ele: anda para baixo do sofá.
            robot.Teleport(new Vector3(3.0f, 0.005f, 2.2f), 0f);
            cam.GetComponent<CameraFollow>().SnapToTarget();
            robot.SetExternalInput(0.5f, 0f);
            yield return new WaitForSeconds(1.2f);
            robot.ClearExternalInput();
            Assert.That(robot.transform.position.z, Is.GreaterThan(2.7f), "o robô entrou embaixo do sofá");
            Assert.That(robot.transform.position.y, Is.LessThan(0.03f), "não subiu no sofá");
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(sofa.IsFaded, "sofá semitransparente");

            robot.Teleport(OpenSpot, 0f);
            cam.GetComponent<CameraFollow>().SnapToTarget();
            yield return new WaitForSeconds(1f);
            Assert.IsFalse(sofa.IsFaded, "sofá volta ao normal");
        }

        [UnityTest]
        public IEnumerator T14_SockTrapsUntilFivePresses()
        {
            ParkCat();
            SockTrap sock = Object.FindObjectsByType<SockTrap>(FindObjectsSortMode.None)[0];
            Vector3 s = sock.transform.position;
            robot.Teleport(new Vector3(s.x, 0.005f, s.z) - Vector3.forward * 0.35f, 0f);
            robot.SetExternalInput(0.5f, 0f);
            for (int i = 0; i < 100 && !robot.IsStuck; i++) yield return new WaitForFixedUpdate();
            Assert.IsTrue(robot.IsStuck, "ficou preso na meia");
            Vector3 p = robot.transform.position;
            yield return new WaitForSeconds(0.5f);
            Assert.That((robot.transform.position - p).magnitude, Is.LessThan(0.02f), "preso não anda");
            for (int i = 0; i < 4; i++) robot.PressUnstick();
            Assert.IsTrue(robot.IsStuck);
            robot.PressUnstick();
            Assert.IsFalse(robot.IsStuck, "5 apertos soltam");
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(robot.IsStuck, "não prende de novo logo em seguida");
            robot.ClearExternalInput();
        }

        [UnityTest]
        public IEnumerator T15_RestartTenTimesResetsEverything()
        {
            for (int i = 0; i < 10; i++)
            {
                // Bagunça o estado antes de reiniciar.
                battery.SetCharge(10f);
                timer.SetRemaining(42f);
                if (i % 2 == 0) gm.TogglePause();
                gm.Restart();
                yield return null;
                yield return null;
                Bind();
                Assert.AreEqual(GameState.Playing, gm.State, $"reinício {i}");
                Assert.AreEqual(1f, Time.timeScale);
                Assert.AreEqual(100f, battery.Charge, 0.5f);
                Assert.That(timer.Remaining, Is.GreaterThan(179f));
                Assert.AreEqual(0, cleaning.Collected);
            }
        }

        [UnityTest]
        public IEnumerator T16_MenuAndBackAgain()
        {
            gm.TogglePause();
            gm.GoToMenu();
            yield return null;
            yield return null;
            Assert.AreEqual(SceneLoader.MainMenu, SceneManager.GetActiveScene().name);
            Assert.AreEqual(1f, Time.timeScale, "menu nunca começa congelado");
            Assert.IsNotNull(Object.FindFirstObjectByType<MainMenu>());
            SceneLoader.Load(SceneLoader.Level01);
            yield return null;
            yield return null;
            Bind();
            Assert.AreEqual(GameState.Playing, gm.State);
        }

        [UnityTest]
        public IEnumerator T17_CatPatrols()
        {
            Vector3 start = cat.transform.position;
            float maxDist = 0f;
            for (float t = 0; t < 8f; t += 0.25f)
            {
                yield return new WaitForSeconds(0.25f);
                maxDist = Mathf.Max(maxDist, Vector3.Distance(start, cat.transform.position));
            }
            Assert.That(maxDist, Is.GreaterThan(0.5f), "o gato anda pela NavMesh");
        }
    }
}
