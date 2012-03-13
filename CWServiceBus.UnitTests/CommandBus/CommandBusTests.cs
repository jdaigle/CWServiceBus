using System;
using System.Reflection;
using CWServiceBus.CommandBus.Messages;
using CWServiceBus.StructureMap;
using NUnit.Framework;
using StructureMap;

namespace CWServiceBus.CommandBus {
    [TestFixture]
    public class CommandBusTests {

        private ICommandBus commandBus;

        [TestFixtureSetUp]
        public void RegisterAssemblyMessageHandlers() {
            var serviceLocator = new StructureMapServiceLocator(new Container(i => {
                i.For<IManagesUnitOfWork>().Use<MyUnitOfWork>();
            }));
            commandBus = CommandBusBuilder.Initialize(i => {
                i.ServiceLocator = serviceLocator;
                i.MessageTypeConventions.AddConvention(x => typeof(ICommand).IsAssignableFrom(x) && x.Namespace == "CWServiceBus.CommandBus.Messages");
                i.AddAssemblyToScan(GetType().Assembly);
            });
        }

        public class MyUnitOfWork : IManagesUnitOfWork {
            public static Action beginCallback;
            public void Begin(IServiceLocator childServiceLocator, IMessageContext messageContext) {
                if (beginCallback != null) beginCallback();
            }

            public static Action<Exception> endCallback;
            public void End(IServiceLocator childServiceLocator, IMessageContext messageContext, Exception exception) {
                if (endCallback != null) endCallback(exception);
            }
        }


        [SetUp]
        public void ClearHandlers() {
            MyUnitOfWork.beginCallback = null;
            MyUnitOfWork.endCallback = null;
            CommandHandler1.callback = null;
            CommandHandler2.callback = null;
            CommandHandler3.callback = null;
            CommandHandler_Generic.callback = null;
        }

        [Test]
        public void Call_UnitOfWork_Begin() {
            var unitOfWorkCalled = false;
            MyUnitOfWork.beginCallback = new Action(() => unitOfWorkCalled = true);
            var actionCalled = false;
            CommandHandler1.callback = new Action<Command1>(x => { Assert.True(unitOfWorkCalled); actionCalled = true; });
            commandBus.Send(new Command1() {
                Data = "Command1",
            });
            Assert.True(unitOfWorkCalled);
            Assert.True(actionCalled);
        }

        [Test]
        public void Call_UnitOfWork_End() {
            var unitOfWorkCalled = false;
            var actionCalled = false;
            CommandHandler1.callback = new Action<Command1>(x => { Assert.False(unitOfWorkCalled); actionCalled = true; });
            MyUnitOfWork.endCallback = new Action<Exception>(e => { Assert.True(actionCalled); unitOfWorkCalled = true; Assert.Null(e); });
            commandBus.Send(new Command1() {
                Data = "Command1",
            });
            Assert.True(actionCalled);
            Assert.True(unitOfWorkCalled);
        }

        [Test]
        public void UnitOfWork_HandlesRollback() {
            var unitOfWorkCalled = false;
            MyUnitOfWork.endCallback = new Action<Exception>(e => {
                unitOfWorkCalled = true;
                Assert.NotNull(e);
                Assert.AreEqual(typeof(ApplicationException), e.GetType());
                Assert.AreEqual("Rollback", e.Message);
            });
            Assert.Throws<TargetInvocationException>(() => {
                commandBus.Send(new Command4() {
                    Data = "Command4",
                });
            });
            Assert.True(unitOfWorkCalled);
        }

        [Test]
        public void Can_Send_Command1() {
            var called = false;
            CommandHandler1.callback = new Action<Command1>(x => called = true);
            commandBus.Send(new Command1() {
                Data = "Command1",
            });
            Assert.True(called);
        }

        [Test]
        public void Can_Send_Command2() {
            var called = false;
            CommandHandler2.callback = new Action<Command2>(x => called = true);
            commandBus.Send(new Command2() {
                Data = "Command2",
            });
            Assert.True(called);
        }

        [Test]
        public void Can_Send_Command3() {
            var called = false;
            CommandHandler3.callback = new Action<ICommand3>(x => called = true);
            commandBus.Send<ICommand3>(a => a.Data = "Command3");
            Assert.True(called);
        }

        [Test]
        public void Can_Send_Generic() {
            var count = 0;
            CommandHandler_Generic.callback = new Action<ICommand>(x => count++);
            commandBus.Send(new Command1() {
                Data = "Command1",
            }, new Command2() {
                Data = "Command2",
            }, commandBus.CreateInstance<ICommand3>(a => a.Data = "Command3"));
            Assert.AreEqual(3, count);
        }
    }
}
