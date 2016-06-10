﻿using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Reactive.Streams.TCK.Tests.Support;
using Reactive.Streams.Utils;

namespace Reactive.Streams.TCK.Tests
{
    /// <summary>
    /// Validates that the TCK's <see cref="SubscriberWhiteboxVerification{T}"/> fails with nice human readable errors.
    /// >Important: Please note that all Publishers implemented in this file are *wrong*!
    /// </summary>
    [TestFixture]
    public class SubscriberWhiteboxVerificationTest : TCKVerificationSupport
    {
        [Test]
        public void required_spec201_mustSignalDemandViaSubscriptionRequest_shouldFailBy_notGettingRequestCall()
        {
            // this mostly verifies the probe is injected correctly
            Func<WhiteboxSubscriberProbe<int?>, ISubscriber<int?>> createSubscriber = probe =>
            {
                return new SimpleSubscriberWithProbe(probe, onSubscribe: subscription =>
                {
                    probe.RegisterOnSubscribe(new SubscriberPuppet(subscription,
                        triggerRequest: _ =>
                        {
                            // forgot to implement request triggering properly!
                        },
                        signalCancel: subscription.Cancel));
                });
            };
            var verification = CustomSubscriberVerification(createSubscriber);
            RequireTestFailure(() => verification.required_spec201_mustSignalDemandViaSubscriptionRequest(),
                "Did not receive expected `Request` call within");
        }

        [Test]
        public void required_spec201_mustSignalDemandViaSubscriptionRequest_shouldPass()
            => SimpleSubscriberVerification().required_spec201_mustSignalDemandViaSubscriptionRequest();

        [Test]
        public void required_spec203_mustNotCallMethodsOnSubscriptionOrPublisherInOnComplete_shouldFail_dueToCallingRequest()
        {
            Func<WhiteboxSubscriberProbe<int?>, ISubscriber<int?>> createSubscriber = probe =>
            {
                ISubscription subs = null;
                return new SimpleSubscriberWithProbe(probe, onSubscribe: subscription =>
                {
                    subs = subscription;
                    probe.RegisterOnSubscribe(NewSimpleSubscriberPuppet(subs));
                }, onComplete: () =>
                {
                    subs.Request(1);
                    probe.RegisterOnComplete();
                });
            };
            var verification = CustomSubscriberVerification(createSubscriber);
            RequireTestFailure(() => verification.required_spec203_mustNotCallMethodsOnSubscriptionOrPublisherInOnComplete(),
                "Subscription.Request MUST NOT be called from Subscriber.OnComplete (Rule 2.3)!");
        }

        [Test]
        public void required_spec203_mustNotCallMethodsOnSubscriptionOrPublisherInOnComplete_shouldFail_dueToCallingCancel()
        {
            Func<WhiteboxSubscriberProbe<int?>, ISubscriber<int?>> createSubscriber = probe =>
            {
                ISubscription subs = null;
                return new SimpleSubscriberWithProbe(probe, onSubscribe: subscription =>
                {
                    subs = subscription;
                    probe.RegisterOnSubscribe(NewSimpleSubscriberPuppet(subs));
                }, onComplete: () =>
                {
                    subs.Cancel();
                    probe.RegisterOnComplete();
                });
            };
            var verification = CustomSubscriberVerification(createSubscriber);
            RequireTestFailure(() => verification.required_spec203_mustNotCallMethodsOnSubscriptionOrPublisherInOnComplete(),
                "Subscription.Cancel MUST NOT be called from Subscriber.OnComplete (Rule 2.3)!");

        }

        [Test]
        public void required_spec203_mustNotCallMethodsOnSubscriptionOrPublisherInOnError_shouldFail_dueToCallingRequest()
        {
            Func<WhiteboxSubscriberProbe<int?>, ISubscriber<int?>> createSubscriber = probe =>
            {
                ISubscription subs = null;
                return new SimpleSubscriberWithProbe(probe, onSubscribe: subscription =>
                {
                    subs = subscription;
                    probe.RegisterOnSubscribe(NewSimpleSubscriberPuppet(subs));
                }, onError: cause =>
                {
                    subs.Request(1);
                });
            };
            var verification = CustomSubscriberVerification(createSubscriber);
            RequireTestFailure(() => verification.required_spec203_mustNotCallMethodsOnSubscriptionOrPublisherInOnError(),
                "Subscription.Request MUST NOT be called from Subscriber.OnError (Rule 2.3)!");
        }

        [Test]
        public void required_spec203_mustNotCallMethodsOnSubscriptionOrPublisherInOnError_shouldFail_dueToCallingCancel()
        {
            Func<WhiteboxSubscriberProbe<int?>, ISubscriber<int?>> createSubscriber = probe =>
            {
                ISubscription subs = null;
                return new SimpleSubscriberWithProbe(probe, onSubscribe: subscription =>
                {
                    subs = subscription;
                    probe.RegisterOnSubscribe(NewSimpleSubscriberPuppet(subs));
                }, onError: cause =>
                {
                    subs.Cancel();
                });
            };
            var verification = CustomSubscriberVerification(createSubscriber);
            RequireTestFailure(() => verification.required_spec203_mustNotCallMethodsOnSubscriptionOrPublisherInOnError(),
                "Subscription.Cancel MUST NOT be called from Subscriber.OnError (Rule 2.3)!");
        }

        [Test]
        public void required_spec205_mustCallSubscriptionCancelIfItAlreadyHasAnSubscriptionAndReceivesAnotherOnSubscribeSignal_shouldFail()
        {
            Func<WhiteboxSubscriberProbe<int?>, ISubscriber<int?>> createSubscriber = probe =>
            {
                return new SimpleSubscriberWithProbe(probe, onSubscribe: subscription =>
                {
                    probe.RegisterOnSubscribe(NewSimpleSubscriberPuppet(subscription));
                    subscription.Request(1);  // this is wrong, as one should always check if should accept or reject the subscription
                });
            };
            var verification = CustomSubscriberVerification(createSubscriber);
            RequireTestFailure(() => verification.required_spec205_mustCallSubscriptionCancelIfItAlreadyHasAnSubscriptionAndReceivesAnotherOnSubscribeSignal(),
                "Expected 2nd Subscription given to subscriber to be cancelled");
        }

        [Test]
        public void required_spec208_mustBePreparedToReceiveOnNextSignalsAfterHavingCalledSubscriptionCancel_shouldFail()
        {
            Func<WhiteboxSubscriberProbe<int?>, ISubscriber<int?>> createSubscriber = probe =>
            {
                var subscriptionCancelled = new AtomicBoolean();

                return new SimpleSubscriberWithProbe(probe,
                    onSubscribe: subscription =>
                    {
                        var puppet = new SubscriberPuppet(subscription,
                            triggerRequest: subscription.Request,
                            signalCancel: () =>
                            {
                                subscriptionCancelled.Value = true;
                                subscription.Cancel();
                            });
                        probe.RegisterOnSubscribe(puppet);
                    },
                    onNext: element =>
                    {
                        if (subscriptionCancelled)
                        {
                            // this is wrong for many reasons, firstly onNext should never throw,
                            // but this test aims to simulate a Subscriber where someone got it's internals wrong and "blows up".
                            throw new Exception("But I thought it's cancelled!");
                        }

                        probe.RegisterOnNext(element);
                    });
            };
            var verification = CustomSubscriberVerification(createSubscriber);
            RequireTestFailure(() => verification.required_spec208_mustBePreparedToReceiveOnNextSignalsAfterHavingCalledSubscriptionCancel(),
                "But I thought it's cancelled!");
        }

        [Test]
        public void required_spec209_mustBePreparedToReceiveAnOnCompleteSignalWithPrecedingRequestCall_shouldFail()
        {
            Func<WhiteboxSubscriberProbe<int?>, ISubscriber<int?>> createSubscriber = probe =>
            {
                return new SimpleSubscriberWithProbe(probe, onComplete: () =>
                {
                    // forgot to call the probe here
                });
            };
            var verification = CustomSubscriberVerification(createSubscriber);
            RequireTestFailure(() => verification.required_spec209_mustBePreparedToReceiveAnOnCompleteSignalWithPrecedingRequestCall(),
                "did not call `RegisterOnComplete()`");
        }

        [Test]
        public void required_spec209_mustBePreparedToReceiveAnOnCompleteSignalWithoutPrecedingRequestCall_shouldPass_withNoopSubscriber()
        {
            Func<WhiteboxSubscriberProbe<int?>, ISubscriber<int?>> createSubscriber = probe =>
            {
                return new SimpleSubscriberWithProbe(probe, onSubscribe: _ =>
                {
                    // intentional omission of probe registration
                });
            };
            var verification = CustomSubscriberVerification(createSubscriber);
            RequireTestFailure(() => verification.required_spec209_mustBePreparedToReceiveAnOnCompleteSignalWithoutPrecedingRequestCall(),
                "did not call `RegisterOnSubscribe`");
        }

        [Test]
        public void required_spec210_mustBePreparedToReceiveAnOnErrorSignalWithPrecedingRequestCall_shouldFail()
        {
            Func<WhiteboxSubscriberProbe<int?>, ISubscriber<int?>> createSubscriber = probe =>
            {
                return new SimpleSubscriberWithProbe(probe, onError: cause =>
                {
                    // this is wrong in many ways (incl. spec violation), but aims to simulate user code which "blows up" when handling the onError signal
                    throw new Exception("Wrong, don't do this!", cause); // intentional spec violation
                });
            };
            var verification = CustomSubscriberVerification(createSubscriber);
            RequireTestFailure(() => verification.required_spec210_mustBePreparedToReceiveAnOnErrorSignalWithPrecedingRequestCall(),
                "Test Exception: Boom!");
        }

        [Test]
        public void required_spec210_mustBePreparedToReceiveAnOnErrorSignalWithoutPrecedingRequestCall_shouldFail()
        {
            Func<WhiteboxSubscriberProbe<int?>, ISubscriber<int?>> createSubscriber = probe =>
            {
                return new SimpleSubscriberWithProbe(probe, onError: cause =>
                {
                    // this is wrong in many ways (incl. spec violation), but aims to simulate user code which "blows up" when handling the onError signal
                    throw new Exception("Wrong, don't do this!", cause); // intentional spec violation
                });
            };
            var verification = CustomSubscriberVerification(createSubscriber);
            RequireTestFailure(() => verification.required_spec210_mustBePreparedToReceiveAnOnErrorSignalWithoutPrecedingRequestCall(),
                "Test Exception: Boom!");
        }

        [Test]
        public void required_spec308_requestMustRegisterGivenNumberElementsToBeProduced_shouldFail()
        {
            // sanity checks the "happy path", that triggerRequest() propagates the right demand
            CustomSubscriberVerification(probe => new SimpleSubscriberWithProbe(probe))
                .required_spec308_requestMustRegisterGivenNumberElementsToBeProduced();
        }


        // FAILING IMPLEMENTATIONS //

        /// <summary>
        /// Verification using a Subscriber that doesn't do anything on any of the callbacks.
        /// 
        /// The <see cref="WhiteboxSubscriberProbe{T}"/> is properly installed in this subscriber.
        /// 
        /// This verification can be used in the "simples case, subscriber which does basically nothing case" validation.
        /// </summary>
        private SubscriberWhiteboxVerification<int?> SimpleSubscriberVerification()
            => new SimpleWhiteboxVerification(new TestEnvironment());

        [TestFixture(Ignore = "Helper verification for single test")]
        private sealed class SimpleWhiteboxVerification : SubscriberWhiteboxVerification<int?>
        {
            /// <summary>
            /// We need this constructor for NUnit even if the fixture is ignored 
            /// </summary>
            public SimpleWhiteboxVerification() : base(new TestEnvironment())
            {

            }

            public SimpleWhiteboxVerification(TestEnvironment environment) : base(environment)
            {
            }

            public override int? CreateElement(int element) => element;

            public override ISubscriber<int?> CreateSubscriber(WhiteboxSubscriberProbe<int?> probe)
            {
                return new LamdaSubscriber<int?>(
                    onSubscribe: subscription => probe.RegisterOnSubscribe(new SubscriberPuppet(subscription)),
                    onNext: probe.RegisterOnNext,
                    onError: probe.RegisterOnError,
                    onComplete: probe.RegisterOnComplete);
            }
        }

        /// <summary>
        /// Verification using a Subscriber that can be fine tuned by the TCK implementer
        /// </summary>
        private SubscriberWhiteboxVerification<int?> CustomSubscriberVerification(
            Func<WhiteboxSubscriberProbe<int?>, ISubscriber<int?>> newSubscriber)
            => new CustomWhiteboxVerification(new TestEnvironment(), newSubscriber);

        [TestFixture(Ignore = "Helper verification for single test")]
        private sealed class CustomWhiteboxVerification : SubscriberWhiteboxVerification<int?>
        {
            private readonly Func<WhiteboxSubscriberProbe<int?>, ISubscriber<int?>> _newSubscriber;

            /// <summary>
            /// We need this constructor for NUnit even if the fixture is ignored 
            /// </summary>
            public CustomWhiteboxVerification() : base(new TestEnvironment())
            {

            }

            public CustomWhiteboxVerification(TestEnvironment environment,
                Func<WhiteboxSubscriberProbe<int?>, ISubscriber<int?>> newSubscriber) : base(environment)
            {
                _newSubscriber = newSubscriber;
            }

            public override int? CreateElement(int element) => element;

            public override ISubscriber<int?> CreateSubscriber(WhiteboxSubscriberProbe<int?> probe)
            {
                try
                {
                    return _newSubscriber(probe);
                }
                catch (Exception ex)
                {
                    throw new Exception("Unable to create subscriber!", ex);
                }
            }
        }

        private static ISubscriberPuppet NewSimpleSubscriberPuppet(ISubscription subscription)
            => new SubscriberPuppet(subscription);

        private sealed class SubscriberPuppet : ISubscriberPuppet
        {
            private readonly Action<long> _triggerRequest;
            private readonly Action _signalCancel;

            public SubscriberPuppet(ISubscription subscription, Action<long> triggerRequest = null, Action signalCancel = null)
            {
                _triggerRequest = triggerRequest ?? subscription.Request;
                _signalCancel = signalCancel ?? subscription.Cancel;
            }

            public void TriggerRequest(long elements) => _triggerRequest(elements);

            public void SignalCancel() => _signalCancel();
        }

        /// <summary>
        /// Simplest possible implementation of Subscriber which calls the WhiteboxProbe in all apropriate places.
        /// Override it to save some lines of boilerplate, and then break behaviour in specific places.
        /// </summary>
        private class SimpleSubscriberWithProbe : ISubscriber<int?>
        {
            private readonly WhiteboxSubscriberProbe<int?> _probe;
            private readonly Action<int?> _onNext;
            private readonly Action<ISubscription> _onSubscribe;
            private readonly Action<Exception> _onError;
            private readonly Action _onComplete;

            public SimpleSubscriberWithProbe(WhiteboxSubscriberProbe<int?> probe, Action<int?> onNext = null,
                Action<ISubscription> onSubscribe = null, Action<Exception> onError = null, Action onComplete = null)
            {
                _probe = probe;
                _onNext = onNext ?? (element => _probe.RegisterOnNext(element));
                _onSubscribe = onSubscribe ??
                               (subscription => _probe.RegisterOnSubscribe(NewSimpleSubscriberPuppet(subscription)));
                _onError = onError ?? (cause => _probe.RegisterOnError(cause)); 
                _onComplete = onComplete ?? (() => _probe.RegisterOnComplete());
            }

            public void OnNext(int? element) => _onNext(element);

            public void OnSubscribe(ISubscription subscription) => _onSubscribe(subscription);

            // Make sure we see the method in the stack trace in release mode 
            [MethodImpl(MethodImplOptions.NoInlining)]
            public void OnComplete() => _onComplete();

            // Make sure we see the method in the stack trace in release mode 
            [MethodImpl(MethodImplOptions.NoInlining)]
            public void OnError(Exception cause) => _onError(cause);
        }
    }
}
