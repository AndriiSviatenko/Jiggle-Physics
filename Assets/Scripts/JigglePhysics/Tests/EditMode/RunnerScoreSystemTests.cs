using CharacterLogic;
using NUnit.Framework;
using UnityEngine;

namespace JigglePhysics.Tests
{
    public class RunnerScoreSystemTests
    {
        private GameObject systemObject;
        private RunnerScoreSystem scoreSystem;

        [SetUp]
        public void SetUp()
        {
            systemObject = new GameObject("TestScoreSystem");
            scoreSystem = systemObject.AddComponent<RunnerScoreSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            if (systemObject != null)
            {
                Object.DestroyImmediate(systemObject);
            }
        }

        [Test]
        public void ScoreSystem_InitializesWithZeroScore()
        {
            Assert.AreEqual(0, scoreSystem.Score);
            Assert.AreEqual(0, scoreSystem.Gems);
            Assert.AreEqual(1, scoreSystem.Combo);
            Assert.AreEqual(0f, scoreSystem.Distance);
        }

        [Test]
        public void ScoreSystem_CollectingGemsIncrementsScoreAndCombo()
        {
            scoreSystem.CollectGem(100);
            Assert.AreEqual(1, scoreSystem.Gems);
            Assert.AreEqual(2, scoreSystem.Combo);
            Assert.AreEqual(200, scoreSystem.Score);

            scoreSystem.CollectGem(100);
            Assert.AreEqual(2, scoreSystem.Gems);
            Assert.AreEqual(3, scoreSystem.Combo);
            Assert.AreEqual(500, scoreSystem.Score);
        }

        [Test]
        public void ScoreSystem_StumbleResetsCombo()
        {
            scoreSystem.CollectGem(100);
            scoreSystem.CollectGem(100);
            Assert.Greater(scoreSystem.Combo, 1);

            scoreSystem.ResetComboOnStumble();
            Assert.AreEqual(1, scoreSystem.Combo);
        }

        [Test]
        public void ScoreSystem_TrackPlayerPositionAddsDistanceAndScore()
        {
            scoreSystem.TrackPlayerPosition(new Vector3(0f, 0f, 0f));
            scoreSystem.TrackPlayerPosition(new Vector3(0f, 0f, 10f));

            Assert.AreEqual(10f, scoreSystem.Distance, 0.1f);
            Assert.Greater(scoreSystem.Score, 0);
        }

        [Test]
        public void ScoreSystem_StuntsAwardPointsMultipliedByCombo()
        {
            scoreSystem.RegisterStunt("ТЕСТ СТАНТ", 200);
            Assert.AreEqual(2, scoreSystem.Combo);
            Assert.AreEqual(400, scoreSystem.Score);
        }
    }
}
