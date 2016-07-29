﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using ReactiveUI.Routing.Actions;
using Splat;
using Xunit;
#pragma warning disable 4014

namespace ReactiveUI.Routing.Tests
{
    public class RouterTests : LocatorTest
    {
        private readonly INavigator navigator;
        private readonly Router router;

        public RouterTests()
        {
            navigator = Substitute.For<INavigator>();
            router = new Router(navigator);
        }

        [Fact]
        public void Test_Ctor_Throws_Exception_When_No_Navigator_Is_Available()
        {
            Assert.Throws<InvalidOperationException>(() => new Router(null));
        }

        [Fact]
        public async Task Test_ShowAsync_Throws_If_Router_Is_Not_Initialized()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await router.DispatchAsync(RouterActions.ShowViewModel(typeof(TestViewModel), new TestParams()));
            });
        }
        
        [Fact]
        public async Task Test_ShowAsync_Pipes_Transition_To_Navigator_If_Router_Actions_Specify_Navigate()
        {
            Locator.CurrentMutable.Register(() => new TestViewModel(), typeof(TestViewModel));
            var initParams = new RouterParams()
            {
                ViewModelMap = new Dictionary<Type, RouteActions>()
                {
                    {
                        typeof(TestViewModel),
                        new RouteActions()
                        {
                            Actions = new [] { RouteActions.Navigate() }
                        }
                    }
                }
            };

            await router.InitAsync(initParams);
            await router.DispatchAsync(RouterActions.ShowViewModel(typeof(TestViewModel), new TestParams()));

            navigator.Received(1)
                .PushAsync(Arg.Is<Transition>(t => t.ViewModel is TestViewModel));
        }

        [Fact]
        public async Task Test_ShowAsync_Does_Not_Pipe_Transition_To_Navigator_If_Router_Actions_Specify_Navigate()
        {
            Locator.CurrentMutable.Register(() => new TestViewModel(), typeof(TestViewModel));
            var initParams = new RouterParams()
            {
                ViewModelMap = new Dictionary<Type, RouteActions>()
                {
                    {
                        typeof(TestViewModel),
                        new RouteActions()
                    }
                }
            };

            await router.InitAsync(initParams);
            await router.DispatchAsync(RouterActions.ShowViewModel(typeof(TestViewModel), new TestParams()));

            navigator.DidNotReceive()
                .PushAsync(Arg.Any<Transition>());
        }

        [Fact]
        public async Task Test_ShowAsync_Throws_If_Given_Type_Is_Not_In_Map()
        {
            var initParams = new RouterParams()
            {
                ViewModelMap = new Dictionary<Type, RouteActions>()
            };

            await router.InitAsync(initParams);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await router.DispatchAsync(RouterActions.ShowViewModel(typeof(TestViewModel), new TestParams()));
            });
        }

        [Fact]
        public async Task Test_ShowAsync_Creates_Presenter_If_Router_Actions_Specify_Present()
        {
            Func<TestPresenterType> presenterConstructor = Substitute.For<Func<TestPresenterType>>();
            presenterConstructor().Returns(new TestPresenterType());
            Locator.CurrentMutable.Register(() => new TestViewModel(), typeof(TestViewModel));
            Locator.CurrentMutable.Register(presenterConstructor, typeof(TestPresenterType));
            var initParams = new RouterParams()
            {
                ViewModelMap = new Dictionary<Type, RouteActions>()
                {
                    {
                        typeof(TestViewModel),
                        new RouteActions()
                        {
                            Actions = new IRouteAction[]
                            {
                                RouteActions.Present(typeof(TestPresenterType))
                            }
                        }
                    }
                }
            };

            await router.InitAsync(initParams);
            await router.DispatchAsync(RouterActions.ShowViewModel(typeof(TestViewModel), new TestParams()));

            presenterConstructor.Received(1)();
        }

        [Fact]
        public async Task Test_ShowAsync_Calls_PresentAsync_On_Created_Presenter()
        {
            IPresenter presenter = Substitute.For<IPresenter>();
            Locator.CurrentMutable.Register(() => new TestViewModel(), typeof(TestViewModel));
            Locator.CurrentMutable.Register(() => presenter, typeof(TestPresenterType));
            var subject = new Subject<Transition>();
            var initParams = new RouterParams()
            {
                ViewModelMap = new Dictionary<Type, RouteActions>()
                {
                    {
                        typeof(TestViewModel),
                        new RouteActions()
                        {
                            Actions = new IRouteAction[]
                            {
                                RouteActions.Present(typeof(TestPresenterType))
                            }
                        }
                    }
                }
            };
            navigator.PushAsync(Arg.Any<Transition>()).Returns(ci =>
            {
                subject.OnNext(ci.Arg<Transition>());
                return Task.FromResult(0);
            });

            await router.InitAsync(initParams);
            await router.DispatchAsync(RouterActions.ShowViewModel(typeof(TestViewModel), new TestParams()));

            presenter.Received(1).PresentAsync(Arg.Any<object>(), Arg.Any<object>());
        }

        [Fact]
        public async Task Test_Router_Presents_Transition_Resolved_From_OnTransition()
        {
            var viewModel = new TestViewModel();
            var subject = new Subject<TransitionEvent>();
            IPresenter presenter = Substitute.For<IPresenter>();
            Locator.CurrentMutable.Register(() => presenter, typeof(TestPresenterType));
            Locator.CurrentMutable.Register(() => viewModel, typeof(TestViewModel));
            var initParams = new RouterParams()
            {
                ViewModelMap = new Dictionary<Type, RouteActions>()
                {
                    {
                        typeof(TestViewModel),
                        new RouteActions()
                        {
                            Actions = new IRouteAction[]
                            {
                                RouteActions.Navigate(),
                                RouteActions.Present(typeof(TestPresenterType))
                            }
                        }
                    }
                }
            };
            navigator.OnTransition.Returns(subject);
            await router.InitAsync(initParams);

            router.ShowAsync<TestViewModel, TestParams>();

            presenter.Received(1).PresentAsync(viewModel, null);
        }

        [Fact]
        public async Task Test_Router_Disposes_Of_Presenters_After_Transition()
        {
            IPresenter presenter = Substitute.For<IPresenter>();
            var disposable = new BooleanDisposable();
            presenter.PresentAsync(Arg.Any<object>(), Arg.Any<object>()).Returns(disposable);
            Locator.CurrentMutable.Register(() => new TestViewModel(), typeof(TestViewModel));
            Locator.CurrentMutable.Register(() => presenter, typeof(TestPresenterType));
            var initParams = new RouterParams()
            {
                ViewModelMap = new Dictionary<Type, RouteActions>()
                {
                    {
                        typeof(TestViewModel),
                        new RouteActions()
                        {
                            Actions = new IRouteAction[]
                            {
                                RouteActions.Navigate(),
                                RouteActions.Present(typeof(TestPresenterType))
                            }
                        }
                    }
                }
            };
            Transition trans = null;
            navigator.PushAsync(Arg.Any<Transition>()).Returns(c =>
            {
                trans = c.Arg<Transition>();
                return Task.FromResult(0);
            });
            navigator.PopAsync().Returns(c => trans);
            await router.InitAsync(initParams);

            await router.ShowAsync<TestViewModel, TestParams>();
            await router.ShowAsync<TestViewModel, TestParams>();

            disposable.IsDisposed.Should().BeFalse();

            await router.BackAsync();
            disposable.IsDisposed.Should().BeTrue();
        }

        [Fact]
        public async Task Test_NavigateBackAction_Causes_Router_To_Navigate_Backwards()
        {
            Resolver.Register(() => new TestViewModel(), typeof(TestViewModel));
            var initParams = new RouterParams()
            {
                ViewModelMap = new Dictionary<Type, RouteActions>()
                {
                    {
                        typeof(TestViewModel),
                        new RouteActions()
                        {
                            Actions = new IRouteAction[]
                            {
                                RouteActions.Navigate()
                            }
                        }
                    }
                }
            };

            await router.InitAsync(initParams);
            await router.ShowAsync<TestViewModel, TestParams>();
            await router.BackAsync();

            navigator.Received(1).PopAsync();
        }

        [Fact]
        public async Task Test_NavigateBackWhileAction_Causes_Rotuer_To_Navigate_Backwards_While_The_Func_Is_True()
        {
            Resolver.Register(() => new TestViewModel(), typeof(TestViewModel));
            var initParams = new RouterParams()
            {
                ViewModelMap = new Dictionary<Type, RouteActions>()
                {
                    {
                        typeof(TestViewModel),
                        new RouteActions()
                        {
                            Actions = new[]
                            {
                                RouteActions.NavigateBackWhile(transition => transition.ViewModel is TestViewModel),
                                RouteActions.Navigate()
                            }
                        }
                    }
                }
            };
            navigator.TransitionStack.Count.Returns(1);
            navigator.Peek().Returns(new Transition()
            {
                ViewModel = new TestViewModel()
            }, new Transition()
            {
                
            });
            await router.InitAsync(initParams);
            await router.ShowAsync<TestViewModel, TestParams>();
            await router.ShowAsync<TestViewModel, TestParams>();

            navigator.Received(1).PopAsync();
        }
    }
}
#pragma warning restore 4014
