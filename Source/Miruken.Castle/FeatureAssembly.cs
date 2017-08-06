﻿namespace Miruken.Castle
{
    using System;
    using System.Reflection;
    using global::Castle.MicroKernel.Registration;
    using global::Castle.MicroKernel.SubSystems.Configuration;
    using global::Castle.Windsor;
    using global::Castle.Windsor.Installer;

    public class FeatureAssembly : IWindsorInstaller
    {
        private bool _skipInstallers;

        public FeatureAssembly(Assembly assembly)
        {
            Assembly = assembly;
        }

        public Assembly        Assembly { get; }
        public Predicate<Type> Filter   { get; private set; }

        public FeatureAssembly Where(Predicate<Type> filter)
        {
            if (filter == null)
                throw new ArgumentNullException(nameof(filter));
            Filter += filter;
            return this;
        }

        public FeatureAssembly SkipInstallers()
        {
            _skipInstallers = true;
            return this;
        }

        void IWindsorInstaller.Install(IWindsorContainer container, IConfigurationStore store)
        {
            var name = Assembly.FullName;
            if (container.Kernel.HasComponent(name)) return;
            if (!_skipInstallers)
                container.Install(FromAssembly.Instance(Assembly));
            container.Register(Component.For<FeatureAssembly>().Instance(this).Named(name));
        }
    }
}
