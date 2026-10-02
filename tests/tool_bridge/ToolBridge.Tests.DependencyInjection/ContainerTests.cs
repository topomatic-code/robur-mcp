using System;
using System.Threading;
using System.Threading.Tasks;
using Topomatic.ToolBridge.DependencyInjection;

namespace ToolBridge.Tests.DependencyInjection
{
    internal sealed class ContainerTests
    {
        [Test]
        internal static void CheckTransientDependencies()
        {
            var container = new Container();
            container.RegisterType<ILeaf, Leaf>();
            container.RegisterType<Branch, Branch>();
            container.RegisterType<Root, Root>();
            var first = container.CreateInstance<Root>();
            var second = container.CreateInstance<Root>();
            Test.Require(first.Branch.Leaf is Leaf, "Nested constructor injection");
            Test.Require(!ReferenceEquals(first, second) && !ReferenceEquals(first.Branch, second.Branch)
                && !ReferenceEquals(first.Branch.Leaf, second.Branch.Leaf), "Fresh instances throughout the graph");
            Test.Require(first.Property == null, "No property injection");
            Test.Throws<ContainerException>(() => container.CreateInstance<Leaf>());
        }

        [Test]
        internal static void CheckRegistrationsDoNotFallBack()
        {
            var container = new Container();
            Test.Throws<ContainerException>(() => container.CreateInstance<ILeaf>());
            Test.Throws<ContainerException>(() => container.GetSingleton<ILeaf>());
            container.RegisterSingleton<ILeaf, Leaf>();
            Test.Throws<ContainerException>(() => container.CreateInstance<ILeaf>());
            container.RegisterType<Branch, Branch>();
            Test.Throws<ContainerException>(() => container.CreateInstance<Branch>());

            var transients = new Container();
            transients.RegisterType<ILeaf, Leaf>();
            Test.Throws<ContainerException>(() => transients.GetSingleton<ILeaf>());
            transients.RegisterType<SingletonBranch, SingletonBranch>();
            Test.Throws<ContainerException>(() => transients.CreateInstance<SingletonBranch>());
        }

        [Test]
        internal static void CheckSingletonAttributeSelectsLifetime()
        {
            var container = new Container();
            container.RegisterType<ILeaf, Leaf>();
            container.RegisterSingleton<ILeaf, Leaf>();
            container.RegisterType<MixedBranch, MixedBranch>();
            var first = container.CreateInstance<MixedBranch>();
            var second = container.CreateInstance<MixedBranch>();
            var singleton = container.GetSingleton<ILeaf>();
            Test.Require(ReferenceEquals(first.Shared, singleton) && ReferenceEquals(second.Shared, singleton),
                "Attributed parameters receive the cached singleton");
            Test.Require(!ReferenceEquals(first.Fresh, second.Fresh) && !ReferenceEquals(first.Fresh, singleton),
                "Unattributed parameters receive independent transient instances");
        }

        [Test]
        internal static void CheckSingletonConstructorDependencies()
        {
            var container = new Container();
            container.RegisterType<ILeaf, Leaf>();
            container.RegisterSingleton<ILeaf, Leaf>();
            container.RegisterSingleton<MixedBranch, MixedBranch>();
            var branch = container.GetSingleton<MixedBranch>();
            Test.Require(ReferenceEquals(branch, container.GetSingleton<MixedBranch>()), "Singleton instance is cached");
            Test.Require(ReferenceEquals(branch.Shared, container.GetSingleton<ILeaf>()), "Nested singleton injection");
            Test.Require(!ReferenceEquals(branch.Fresh, branch.Shared), "Transient dependency inside a singleton stays separate");

            var other = new Container();
            other.RegisterSingleton<ILeaf, Leaf>();
            Test.Require(!ReferenceEquals(branch.Shared, other.GetSingleton<ILeaf>()), "Singleton belongs to its container");
        }

        [Test]
        internal static void CheckFactoriesWithoutContainer()
        {
            var container = new Container();
            var transientCalls = 0;
            var singletonCalls = 0;
            container.RegisterType<ILeaf>(() => { transientCalls++; return new Leaf(); });
            container.RegisterSingleton<ILeaf>(() => { singletonCalls++; return new Leaf(); });
            Test.Require(transientCalls == 0 && singletonCalls == 0, "Registration does not invoke factories");
            var first = container.CreateInstance<ILeaf>();
            var second = container.CreateInstance<ILeaf>();
            var singleton = container.GetSingleton<ILeaf>();
            Test.Require(ReferenceEquals(singleton, container.GetSingleton<ILeaf>()), "Singleton factory result is cached");
            Test.Require(!ReferenceEquals(first, second) && !ReferenceEquals(first, singleton), "Factory lifetimes stay separate");
            Test.Require(transientCalls == 2 && singletonCalls == 1, "Factory invocation counts match lifetimes");
        }

        [Test]
        internal static void CheckFactoriesWithContainer()
        {
            var container = new Container();
            IContainer received = null;
            container.RegisterSingleton<IContainer>(c => c);
            container.RegisterSingleton<ILeaf, Leaf>();
            container.RegisterType<Branch>(c =>
            {
                received = c;
                return new Branch(c.GetSingleton<ILeaf>());
            });
            container.RegisterSingleton<Root>(c => new Root(c.CreateInstance<Branch>()));
            var root = container.GetSingleton<Root>();
            Test.Require(ReferenceEquals(received, container), "Factory receives the owning container");
            Test.Require(ReferenceEquals(container.GetSingleton<IContainer>(), container), "Factory can supply an existing instance");
            Test.Require(ReferenceEquals(root.Branch.Leaf, container.GetSingleton<ILeaf>()), "Factory resolves its dependencies");
            Test.Require(ReferenceEquals(root, container.GetSingleton<Root>()), "Container-aware singleton factory is cached");
            Test.Require(!ReferenceEquals(root.Branch, container.CreateInstance<Branch>()), "Container-aware transient factory runs again");
        }

        [Test]
        internal static void CheckConcurrentSingletonFactory()
        {
            var container = new Container();
            var calls = 0;
            container.RegisterSingleton<ILeaf>(() =>
            {
                Interlocked.Increment(ref calls);
                return new Leaf();
            });
            var instances = new ILeaf[32];
            // The Robur test context is accessed only on the calling thread.
            Parallel.For(0, instances.Length, i => instances[i] = container.GetSingleton<ILeaf>());
            Test.Require(calls == 1, "Concurrent requests invoke the factory once");
            foreach (var instance in instances)
                Test.Require(instance != null && ReferenceEquals(instance, instances[0]), "Concurrent requests share one instance");
        }

        [Test]
        internal static void CheckDuplicateRegistrations()
        {
            // Both orders: implementation -> factory and factory -> implementation.
            foreach (var factoryFirst in new[] { false, true })
            {
                var container = new Container();
                if (factoryFirst)
                {
                    container.RegisterType<ILeaf>(() => new Leaf());
                    container.RegisterSingleton<ILeaf>(() => new Leaf());
                }
                else
                {
                    container.RegisterType<ILeaf, Leaf>();
                    container.RegisterSingleton<ILeaf, Leaf>();
                }
                var singleton = container.GetSingleton<ILeaf>();
                Test.Throws<ContainerException>(() => container.RegisterType<ILeaf, OtherLeaf>());
                Test.Throws<ContainerException>(() => container.RegisterSingleton<ILeaf, OtherLeaf>());
                Test.Throws<ContainerException>(() => container.RegisterType<ILeaf>(() => new OtherLeaf()));
                Test.Throws<ContainerException>(() => container.RegisterSingleton<ILeaf>(() => new OtherLeaf()));
                Test.Throws<ContainerException>(() => container.RegisterType<ILeaf>(c => new OtherLeaf()));
                Test.Throws<ContainerException>(() => container.RegisterSingleton<ILeaf>(c => new OtherLeaf()));
                Test.Require(container.CreateInstance<ILeaf>() is Leaf, "Duplicate registration preserves the original transient");
                Test.Require(ReferenceEquals(singleton, container.GetSingleton<ILeaf>()), "Duplicate registration preserves the cached singleton");
            }
        }

        [Test]
        internal static void CheckMissingDependencyDiagnosticsAndRetry()
        {
            var container = new Container();
            container.RegisterSingleton<Root, Root>();
            container.RegisterType<Branch, Branch>();
            var error = Test.Throws<ContainerException>(() => container.GetSingleton<Root>());
            Test.RequireContains(error.Message, typeof(Root).ToString(), typeof(Branch).ToString(), typeof(ILeaf).ToString(),
                "Singleton", "Transient", "parameter: branch", "parameter: leaf", "Resolution path:");
            var path = error.Message.Substring(error.Message.IndexOf("Resolution path:", StringComparison.Ordinal));
            Test.Require(path.IndexOf(typeof(Root).ToString(), StringComparison.Ordinal)
                < path.IndexOf(typeof(Branch).ToString(), StringComparison.Ordinal)
                && path.IndexOf(typeof(Branch).ToString(), StringComparison.Ordinal)
                < path.IndexOf(typeof(ILeaf).ToString(), StringComparison.Ordinal), "Diagnostic path follows dependency order");
            container.RegisterType<ILeaf, Leaf>();
            var root = container.GetSingleton<Root>();
            Test.Require(root.Branch.Leaf is Leaf && ReferenceEquals(root, container.GetSingleton<Root>()),
                "Failed singleton creation can be retried after registering the dependency");
        }

        [Test]
        internal static void CheckFactoryFailuresAndRetry()
        {
            var container = new Container();
            var cause = new InvalidOperationException("Factory failure marker");
            var calls = 0;
            container.RegisterSingleton<ILeaf>(() =>
            {
                if (++calls == 1) throw cause;
                return new Leaf();
            });
            var error = Test.Throws<ContainerException>(() => container.GetSingleton<ILeaf>());
            Test.Require(ReferenceEquals(error.InnerException, cause), "Original factory exception is retained");
            Test.RequireContains(error.Message, cause.Message, typeof(ILeaf).ToString(), "Singleton", "Resolution path:");
            var singleton = container.GetSingleton<ILeaf>();
            Test.Require(ReferenceEquals(singleton, container.GetSingleton<ILeaf>()) && calls == 2, "Only a successful factory result is cached");
        }

        [Test]
        internal static void CheckNullFactoryResults()
        {
            var container = new Container();
            var calls = 0;
            container.RegisterType<ILeaf>(() => null);
            container.RegisterSingleton<ILeaf>(() => ++calls == 1 ? null : new Leaf());
            Test.RequireContains(Test.Throws<ContainerException>(() => container.CreateInstance<ILeaf>()).Message, typeof(ILeaf).ToString(), "null", "Transient");
            Test.RequireContains(Test.Throws<ContainerException>(() => container.GetSingleton<ILeaf>()).Message, typeof(ILeaf).ToString(), "null", "Singleton");
            var singleton = container.GetSingleton<ILeaf>();
            Test.Require(singleton != null && ReferenceEquals(singleton, container.GetSingleton<ILeaf>()) && calls == 2,
                "Null is not cached as a singleton");
        }

        [Test]
        internal static void CheckConstructorFailureDiagnostics()
        {
            var container = new Container();
            container.RegisterType<ThrowingConstructor, ThrowingConstructor>();
            var error = Test.Throws<ContainerException>(() => container.CreateInstance<ThrowingConstructor>());
            Test.Require(error.InnerException is InvalidOperationException, "Constructor exception is unwrapped from reflection");
            Test.RequireContains(error.InnerException.Message, "Constructor failure marker");
            Test.RequireContains(error.Message, typeof(ThrowingConstructor).ToString(), "Constructor failure marker", "Resolution path:");
        }

        [Test]
        internal static void CheckConstructorCycles()
        {
            var container = new Container();
            container.RegisterType<CycleA, CycleA>();
            container.RegisterType<CycleB, CycleB>();
            var error = Test.Throws<ContainerException>(() => container.CreateInstance<CycleA>());
            Test.RequireContains(error.Message, "Circular", typeof(CycleA).ToString(), typeof(CycleB).ToString());
            container.RegisterSingleton<SelfSingleton, SelfSingleton>();
            Test.RequireContains(Test.Throws<ContainerException>(() => container.GetSingleton<SelfSingleton>()).Message, "Circular", "Singleton");
            container.RegisterType<ILeaf, Leaf>();
            Test.Require(container.CreateInstance<ILeaf>() is Leaf, "Cycle failure does not poison later resolutions");
        }

        [Test]
        internal static void CheckFactoryCyclesAndLifetimeSeparation()
        {
            var container = new Container();
            container.RegisterType<ILeaf>(c => c.CreateInstance<Branch>().Leaf);
            container.RegisterType<Branch>(c => new Branch(c.CreateInstance<ILeaf>()));
            Test.RequireContains(Test.Throws<ContainerException>(() => container.CreateInstance<ILeaf>()).Message,
                "Circular", typeof(ILeaf).ToString(), typeof(Branch).ToString());
            container.RegisterSingleton<ILeaf>(c => c.GetSingleton<ILeaf>());
            Test.RequireContains(Test.Throws<ContainerException>(() => container.GetSingleton<ILeaf>()).Message, "Circular", "Singleton");

            var separate = new Container();
            separate.RegisterType<ILeaf, Leaf>();
            separate.RegisterSingleton<ILeaf>(c => c.CreateInstance<ILeaf>());
            Test.Require(separate.GetSingleton<ILeaf>() is Leaf, "Same service with a different lifetime is not a cycle");
            Test.Require(!ReferenceEquals(separate.GetSingleton<ILeaf>(), separate.CreateInstance<ILeaf>()), "Factory-created singleton remains separate");
        }

        [Test]
        internal static void CheckConstructorSelection()
        {
            var container = new Container();
            container.RegisterType<Greedy, Greedy>();
            // A parameterless constructor must not hide a missing dependency.
            Test.Throws<ContainerException>(() => container.CreateInstance<Greedy>());
            container.RegisterType<ILeaf, Leaf>();
            Test.Require(container.CreateInstance<Greedy>().Leaf is Leaf, "Longest public constructor is used");
            container.RegisterType<Ambiguous, Ambiguous>();
            Test.Throws<ContainerException>(() => container.CreateInstance<Ambiguous>());
            container.RegisterType<PrivateConstructor, PrivateConstructor>();
            Test.Throws<ContainerException>(() => container.CreateInstance<PrivateConstructor>());
            container.RegisterType<Optional, Optional>();
            Test.Throws<ContainerException>(() => container.CreateInstance<Optional>());
            container.RegisterType<string>(() => "registered value");
            Test.Require(container.CreateInstance<Optional>().Value == "registered value", "Optional parameters are resolved through registrations");
        }

        [Test]
        internal static void CheckInvalidRegistrations()
        {
            var container = new Container();
            Test.Throws<ArgumentException>(() => container.RegisterType<ILeaf, ILeaf>());
            Test.Throws<ArgumentException>(() => container.RegisterSingleton<ILeaf, ILeaf>());
            Test.Throws<ArgumentException>(() => container.RegisterType<Abstract, Abstract>());
            Test.Throws<ArgumentException>(() => container.RegisterSingleton<Abstract, Abstract>());
            Test.Throws<ArgumentNullException>(() => container.RegisterType<ILeaf>((Func<ILeaf>)null));
            Test.Throws<ArgumentNullException>(() => container.RegisterType<ILeaf>((Func<IContainer, ILeaf>)null));
            Test.Throws<ArgumentNullException>(() => container.RegisterSingleton<ILeaf>((Func<ILeaf>)null));
            Test.Throws<ArgumentNullException>(() => container.RegisterSingleton<ILeaf>((Func<IContainer, ILeaf>)null));
        }

        public interface ILeaf { }
        public sealed class Leaf : ILeaf { }
        public sealed class OtherLeaf : ILeaf { }
        public sealed class Branch
        {
            public readonly ILeaf Leaf;
            public Branch(ILeaf leaf) { Leaf = leaf; }
        }
        public sealed class SingletonBranch
        {
            public SingletonBranch([Singleton] ILeaf leaf) { }
        }
        public sealed class MixedBranch
        {
            public readonly ILeaf Fresh;
            public readonly ILeaf Shared;
            public MixedBranch(ILeaf fresh, [Singleton] ILeaf shared) { Fresh = fresh; Shared = shared; }
        }
        public sealed class Root
        {
            public readonly Branch Branch;
            public ILeaf Property { get; set; }
            public Root(Branch branch) { Branch = branch; }
        }
        public sealed class CycleA { public CycleA(CycleB b) { } }
        public sealed class CycleB { public CycleB(CycleA a) { } }
        public sealed class SelfSingleton { public SelfSingleton([Singleton] SelfSingleton self) { } }
        public sealed class ThrowingConstructor
        {
            public ThrowingConstructor() { throw new InvalidOperationException("Constructor failure marker"); }
        }
        public sealed class Greedy
        {
            public readonly ILeaf Leaf;
            public Greedy() { }
            public Greedy(ILeaf leaf) { Leaf = leaf; }
        }
        public sealed class Ambiguous
        {
            public Ambiguous(ILeaf leaf) { }
            public Ambiguous(Branch branch) { }
        }
        public sealed class PrivateConstructor { private PrivateConstructor() { } }
        public sealed class Optional
        {
            public readonly string Value;
            public Optional(string value = "default") { Value = value; }
        }
        public abstract class Abstract { }
    }
}
