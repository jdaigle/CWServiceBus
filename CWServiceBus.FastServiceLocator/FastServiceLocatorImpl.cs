using System;
using System.Collections.Generic;
using CWServiceBus;

namespace CWServiceBus.FastServiceLocator
{
    public class FastServiceLocatorImpl : IServiceLocator
    {
        private readonly FastContainer container;

        public FastServiceLocatorImpl(FastContainer container)
        {
            this.container = container;
        }

        public void BuildUp(object target)
        {
            container.FillProperties(target);
        }

        public T Get<T>()
        {
            var instance = container.Resolve<T>();
            if (instance == null)
            {
                if (typeof(T).GetConstructor(new Type[0]) == null)
                {
                    throw new MissingMethodException("Missing Default Parameterless Constructor For Type: " + typeof(T));
                }
                instance = Activator.CreateInstance<T>();
            }
            container.FillProperties(instance);
            return instance;
        }

        public IEnumerable<T> GetAll<T>()
        {
            yield return container.Resolve<T>();
        }

        public object Get(Type type)
        {
            var instance = container.Resolve(type);
            if (instance == null)
            {
                if (type.GetConstructor(new Type[0]) == null)
                {
                    throw new MissingMethodException("Missing Default Parameterless Constructor For Type: " + type);
                }
                instance = Activator.CreateInstance(type);
            }
            container.FillProperties(instance);
            return instance;
        }

        public IEnumerable<object> GetAll(Type type)
        {
            yield return container.Resolve(type);
        }

        public void RegisterComponent<T>(T instance)
        {
            container.Register<T>(instance);
        }

        public void RegisterComponent(Type type, object instance)
        {
            container.Register(type, instance);
        }

        public IServiceLocator GetChildServiceLocator()
        {
            return new FastServiceLocatorImpl(this.container.Clone());
        }

        public void Dispose()
        {
            container.Dispose();
        }
    }
}
