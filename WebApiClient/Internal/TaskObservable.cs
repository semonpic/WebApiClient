﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebApiClient
{
    /// <summary>
    /// 表示任务的Rx
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    class TaskObservable<TResult> : ITaskObservable<TResult>
    {
        /// <summary>
        /// 任务
        /// </summary>
        private readonly Task<TResult> task;

        /// <summary>
        /// 观察者列表
        /// </summary>
        private readonly ObserverTable<TResult> observerTable = new ObserverTable<TResult>();

        /// <summary>
        /// 任务的Rx
        /// </summary>
        /// <param name="task">任务</param>
        public TaskObservable(Task<TResult> task)
        {
            this.task = task;
            this.task.ContinueWith(this.OnTaskCompleted);
        }

        /// <summary>
        /// 任务完成时
        /// </summary>
        /// <param name="t"></param>
        private void OnTaskCompleted(Task<TResult> t)
        {
            switch (t.Status)
            {
                case TaskStatus.RanToCompletion:
                    this.observerTable.RaiseNextAndCompleted(t.Result);
                    break;

                case TaskStatus.Faulted:
                    this.observerTable.RaiseErrorAndCompleted(t.Exception.InnerException);
                    break;

                case TaskStatus.Canceled:
                    this.observerTable.RaiseErrorAndCompleted(new TaskCanceledException(t));
                    break;
            }
        }

        /// <summary>
        /// 响应观察者
        /// </summary>
        /// <param name="observer"></param>
        /// <param name="t"></param>
        private void RaiseObserver(IObserver<TResult> observer, Task<TResult> t)
        {
            switch (t.Status)
            {
                case TaskStatus.RanToCompletion:
                    observer.OnNext(t.Result);
                    observer.OnCompleted();
                    break;

                case TaskStatus.Faulted:
                    observer.OnError(t.Exception.InnerException);
                    observer.OnCompleted();
                    break;

                case TaskStatus.Canceled:
                    observer.OnError(new TaskCanceledException(t));
                    observer.OnCompleted();
                    break;
            }
        }

        /// <summary>
        /// 订阅
        /// </summary>
        /// <param name="onResult">收到结果委托</param>
        /// <param name="onError">遇到错误委托</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <returns></returns>
        public IDisposable Subscribe(Action<TResult> onResult, Action<Exception> onError)
        {
            var observer = new TaskObserver<TResult>(onResult, onError, null);
            return this.Subscribe(observer);
        }

        /// <summary>
        /// 订阅
        /// </summary>
        /// <param name="observer">观察者</param>
        /// <returns></returns>
        public IDisposable Subscribe(IObserver<TResult> observer)
        {
            if (this.observerTable.Add(observer) == true)
            {
                return new Unsubscriber<TResult>(() => this.observerTable.Remove(observer));
            }
            else
            {
                this.RaiseObserver(observer, this.task);
                return new Unsubscriber<TResult>(null);
            }
        }



        /// <summary>
        /// 表示观察者列表
        /// 线程安全类型 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        private class ObserverTable<T>
        {
            /// <summary>
            /// 是否触发过
            /// </summary>
            private bool raised = false;

            /// <summary>
            /// 同步锁
            /// </summary>
            private readonly object syncRoot = new object();

            /// <summary>
            /// 观察者列表
            /// </summary>
            private readonly List<IObserver<T>> observers = new List<IObserver<T>>();

            /// <summary>
            /// 添加观察者
            /// </summary>
            /// <param name="observer">观察者</param>
            /// <returns></returns>
            public bool Add(IObserver<T> observer)
            {
                lock (this.syncRoot)
                {
                    if (this.raised == false)
                    {
                        this.observers.Add(observer);
                        return true;
                    }
                    return false;
                }
            }

            /// <summary>
            /// 移除观察者
            /// </summary>
            /// <param name="observer">观察者</param>
            public void Remove(IObserver<T> observer)
            {
                lock (this.syncRoot)
                {
                    this.observers.Remove(observer);
                }
            }

            /// <summary>
            /// 触发有结果值到所有观察者
            /// </summary>
            /// <param name="value">结果值</param>
            public void RaiseNextAndCompleted(T value)
            {
                lock (this.syncRoot)
                {
                    this.raised = true;
                    foreach (var item in this.observers)
                    {
                        item.OnNext(value);
                        item.OnCompleted();
                    }
                    this.observers.Clear();
                }
            }

            /// <summary>
            /// 触发有异常到所有观察者
            /// </summary>
            /// <param name="ex">异常</param>
            public void RaiseErrorAndCompleted(Exception ex)
            {
                lock (this.syncRoot)
                {
                    this.raised = true;
                    foreach (var item in this.observers)
                    {
                        item.OnError(ex);
                        item.OnCompleted();
                    }
                    this.observers.Clear();
                }
            }
        }

        /// <summary>
        /// 表示订阅取消器
        /// </summary>
        /// <typeparam name="T"></typeparam>
        private class Unsubscriber<T> : IDisposable
        {
            /// <summary>
            /// 取消委托
            /// </summary>
            private readonly Action onUnsubscribe;

            /// <summary>
            /// 订阅取消器
            /// </summary>
            /// <param name="onUnsubscribe">取消委托</param>
            public Unsubscriber(Action onUnsubscribe)
            {
                this.onUnsubscribe = onUnsubscribe;
            }

            /// <summary>
            /// 取消订阅
            /// </summary>
            public void Dispose()
            {
                this.onUnsubscribe?.Invoke();
            }
        }
    }
}
