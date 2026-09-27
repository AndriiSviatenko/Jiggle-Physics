using System;
using System.Collections.Generic;
using UnityEngine;

namespace ComponentLogic
{
    public class ComponentRepository
    {
        private readonly Dictionary<Type, IComponent> components = new Dictionary<Type, IComponent>();
        private readonly List<ITickableComponent> tickables = new List<ITickableComponent>();
        private readonly List<IFixedTickableComponent> fixedTickables = new List<IFixedTickableComponent>();
        private readonly List<ILateTickableComponent> lateTickables = new List<ILateTickableComponent>();
        private readonly List<IPlayableComponent> playables = new List<IPlayableComponent>();
        private readonly List<IDestroyableComponent> destroyables = new List<IDestroyableComponent>();

        public int Count
        {
            get { return components.Count; }
        }

        public bool TryGet<T>(out T component) where T : class, IComponent
        {
            component = Get<T>();
            return component != null;
        }

        public T Get<T>() where T : class, IComponent
        {
            IComponent stored;
            if (components.TryGetValue(typeof(T), out stored))
            {
                return stored as T;
            }

            return null;
        }

        public void Add<T>(T component) where T : IComponent
        {
            if (component == null || components.ContainsKey(typeof(T)))
            {
                return;
            }

            components.Add(typeof(T), component);
            CacheInterfaces(component);
        }

        public void Remove<T>() where T : IComponent
        {
            IComponent stored;
            if (!components.TryGetValue(typeof(T), out stored))
            {
                return;
            }

            UncacheInterfaces(stored);

            IDestroyableComponent destroyable = stored as IDestroyableComponent;
            if (destroyable != null)
            {
                destroyable.Destroy();
            }

            components.Remove(typeof(T));
        }

        public void RemoveAll()
        {
            foreach (IComponent component in components.Values)
            {
                IPlayableComponent playable = component as IPlayableComponent;
                if (playable != null)
                {
                    playable.Stop();
                }

                IDestroyableComponent destroyable = component as IDestroyableComponent;
                if (destroyable != null)
                {
                    destroyable.Destroy();
                }
            }

            components.Clear();
            tickables.Clear();
            fixedTickables.Clear();
            lateTickables.Clear();
            playables.Clear();
            destroyables.Clear();
        }

        public void Tick()
        {
            float deltaTime = Time.deltaTime;
            for (int i = 0; i < tickables.Count; i++)
            {
                ITickableComponent component = tickables[i];
                if (component == null)
                {
                    continue;
                }

                try
                {
                    component.Tick(deltaTime);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[ComponentRepository] Tick failed in {component.GetType().Name}: {exception}");
                }
            }
        }

        public void FixedTick()
        {
            float deltaTime = Time.fixedDeltaTime;
            for (int i = 0; i < fixedTickables.Count; i++)
            {
                IFixedTickableComponent component = fixedTickables[i];
                if (component == null)
                {
                    continue;
                }

                try
                {
                    component.FixedTick(deltaTime);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[ComponentRepository] FixedTick failed in {component.GetType().Name}: {exception}");
                }
            }
        }

        public void LateTick()
        {
            float deltaTime = Time.deltaTime;
            for (int i = 0; i < lateTickables.Count; i++)
            {
                ILateTickableComponent component = lateTickables[i];
                if (component == null)
                {
                    continue;
                }

                try
                {
                    component.LateTick(deltaTime);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[ComponentRepository] LateTick failed in {component.GetType().Name}: {exception}");
                }
            }
        }

        public void Play()
        {
            for (int i = 0; i < playables.Count; i++)
            {
                IPlayableComponent playable = playables[i];
                if (playable == null)
                {
                    continue;
                }

                try
                {
                    playable.Play();
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[ComponentRepository] Play failed in {playable.GetType().Name}: {exception}");
                }
            }
        }

        public void Stop()
        {
            for (int i = 0; i < playables.Count; i++)
            {
                IPlayableComponent playable = playables[i];
                if (playable == null)
                {
                    continue;
                }

                try
                {
                    playable.Stop();
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[ComponentRepository] Stop failed in {playable.GetType().Name}: {exception}");
                }
            }
        }

        private void CacheInterfaces(IComponent component)
        {
            ITickableComponent tickable = component as ITickableComponent;
            if (tickable != null && !tickables.Contains(tickable))
            {
                tickables.Add(tickable);
            }

            IFixedTickableComponent fixedTickable = component as IFixedTickableComponent;
            if (fixedTickable != null && !fixedTickables.Contains(fixedTickable))
            {
                fixedTickables.Add(fixedTickable);
            }

            ILateTickableComponent lateTickable = component as ILateTickableComponent;
            if (lateTickable != null && !lateTickables.Contains(lateTickable))
            {
                lateTickables.Add(lateTickable);
            }

            IPlayableComponent playable = component as IPlayableComponent;
            if (playable != null && !playables.Contains(playable))
            {
                playables.Add(playable);
            }

            IDestroyableComponent destroyable = component as IDestroyableComponent;
            if (destroyable != null && !destroyables.Contains(destroyable))
            {
                destroyables.Add(destroyable);
            }
        }

        private void UncacheInterfaces(IComponent component)
        {
            ITickableComponent tickable = component as ITickableComponent;
            if (tickable != null)
            {
                tickables.Remove(tickable);
            }

            IFixedTickableComponent fixedTickable = component as IFixedTickableComponent;
            if (fixedTickable != null)
            {
                fixedTickables.Remove(fixedTickable);
            }

            ILateTickableComponent lateTickable = component as ILateTickableComponent;
            if (lateTickable != null)
            {
                lateTickables.Remove(lateTickable);
            }

            IPlayableComponent playable = component as IPlayableComponent;
            if (playable != null)
            {
                playables.Remove(playable);
            }

            IDestroyableComponent destroyable = component as IDestroyableComponent;
            if (destroyable != null)
            {
                destroyables.Remove(destroyable);
            }
        }
    }
}
