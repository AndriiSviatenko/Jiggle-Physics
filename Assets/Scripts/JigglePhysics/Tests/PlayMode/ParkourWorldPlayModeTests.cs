using System.Collections;
using CharacterLogic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace JigglePhysics.Tests.PlayMode
{
    public class ParkourWorldPlayModeTests
    {
        [UnityTest]
        public IEnumerator ParkourCity_BootstrapsPlayableCourseAndCharacter()
        {
            SceneManager.LoadScene("ParkourCity");
            yield return null;
            yield return null;

            GameObject world = GameObject.Find("Parkour World");
            Assert.That(world, Is.Not.Null, "Runtime parkour course was not created.");
            Assert.That(world.transform.childCount, Is.GreaterThan(20), "Course is missing gameplay geometry.");
            Assert.That(GameObject.Find("Level Portal"), Is.Not.Null, "Level transition portal is missing.");

            CharacterBridge bridge = Object.FindFirstObjectByType<CharacterBridge>();
            Assert.That(bridge, Is.Not.Null, "Character bridge is missing from the gameplay scene.");
            Assert.That(bridge.Mover, Is.Not.Null, "Parkour controller was not initialized.");
            Assert.That(Object.FindFirstObjectByType<ParkourHUD>(), Is.Not.Null, "Gameplay HUD was not initialized.");
            Assert.That(Object.FindFirstObjectByType<ParkourPresentation>(), Is.Not.Null, "Movement presentation layer was not initialized.");
            Assert.That(GameObject.Find("Velocity Trail"), Is.Not.Null, "Speed trail visual is missing.");
            Assert.That(GameObject.Find("Wall Sparks"), Is.Not.Null, "Wall-run spark system is missing.");
            Assert.That(GameObject.Find("Speed Wisps"), Is.Not.Null, "Speed-line particle system is missing.");

            Animator activeAnimator = null;
            int enabledAnimators = 0;
            foreach (Animator candidate in bridge.GetComponentsInChildren<Animator>(true))
            {
                if (!candidate.enabled) continue;
                enabledAnimators++;
                if (candidate.runtimeAnimatorController != null) activeAnimator = candidate;
            }
            Assert.That(enabledAnimators, Is.EqualTo(1), "Multiple enabled Animators are fighting over the character rig.");
            Assert.That(activeAnimator, Is.Not.Null, "The playable character has no active Animator Controller.");
            Assert.That(activeAnimator.avatar, Is.Not.Null, "The active Animator has no humanoid Avatar and will fall into a T-pose.");
            Assert.That(activeAnimator.avatar.isValid && activeAnimator.avatar.isHuman, Is.True, "The active Animator Avatar is not a valid humanoid.");
            Assert.That(activeAnimator.transform, Is.Not.EqualTo(bridge.transform), "The visual Animator must not rotate the CharacterController root.");
            Assert.That(activeAnimator.gameObject.name, Is.EqualTo("Visual Animation Root"), "Animator must own the complete model hierarchy, not only the Armature subtree.");
            Assert.That(activeAnimator.isInitialized, Is.True, "The visual Animator failed to bind to the character hierarchy.");
            Assert.That(activeAnimator.isHuman, Is.True, "The visual Animator is not running in humanoid retargeting mode.");
        }
    }
}
