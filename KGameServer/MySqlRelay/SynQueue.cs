using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace MySqlRelay
{
    public class SynQueue<T>
    {
        #region "Variables"

        private List<T> qlist;
        private AutoResetEvent ars;
        private AutoResetEvent ars2;

        public int toSentCount = 0;
        public int eraseCount = 0;

        #endregion

        public SynQueue()
        {
            qlist = new List<T>();
            ars = new AutoResetEvent(true);
            ars2 = new AutoResetEvent(false);
        }

        /// <summary>
        /// 入队，如果前面有相同的内容，则不入队
        /// </summary>
        /// <param name="t"></param>
        public void Enqueue(T t, Boolean eraseSameBefore)
        {
            if (t == null) return;
            ars.WaitOne(Timeout.Infinite, true);
            if (eraseSameBefore)
            {
                try
                {
                    Boolean bFound = false;
                    Console.WriteLine("qlist的个数为:" + qlist.Count.ToString());
                    for (int i = 0; i < qlist.Count; i++)
                    {
                        if (qlist[i] != null)
                        {
                            if (qlist[i].Equals(t))
                            {
                                this.eraseCount += 1;
                                Console.WriteLine("发现相同的包，删除 eraseCount=" + this.eraseCount.ToString() + " toSentCount=" + toSentCount.ToString());
                                bFound = true;
                            }
                        }
                    }
                    if (bFound == false)
                    {
                        qlist.Add(t);
                        ars2.Set();
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
            else
            {
                qlist.Add(t);
                ars2.Set();
            }
            ars.Set();
        }

        /// <summary>
        /// 出队，如果没内容则阻塞
        /// </summary>
        /// <returns></returns>
        public T Dequeue()
        {
            ars.WaitOne(Timeout.Infinite, true);
            while (qlist.Count == 0)
            {
                ars.Set();
                ars2.WaitOne(Timeout.Infinite, true);
                ars.WaitOne(Timeout.Infinite, true);
            }
            T ret = qlist.ElementAt(0);
            qlist.RemoveAt(0);
            ars.Set();
            return ret;
        }

        /// <summary>
        /// 取得队首元素，如果没内容则阻塞
        /// </summary>
        /// <returns></returns>
        public T Peek()
        {
            ars.WaitOne(Timeout.Infinite, true);
            while (qlist.Count == 0)
            {
                ars.Set();
                ars2.WaitOne(Timeout.Infinite, true);
                ars.WaitOne(Timeout.Infinite, true);
            }
            T ret = qlist.ElementAt(0);
            ars.Set();
            return ret;
        }

        /// <summary>
        /// 取得队首元素，如果没内容则阻塞
        /// </summary>
        /// <returns></returns>
        public T Peek(int interval)
        {
            ars.WaitOne(Timeout.Infinite, true);
            ars2.WaitOne(interval, true);
            T ret = default(T);
            if (qlist.Count > 0)
            {
                ret = qlist.ElementAt(0);
            }
            ars.Set();
            return ret;
        }

        /// <summary>
        /// 全部出队，如果没有则阻塞
        /// </summary>
        /// <returns></returns>
        public List<T> DequeueAll(int timeout = Timeout.Infinite)
        {
            ars.WaitOne(Timeout.Infinite, true);
            while (qlist.Count == 0)
            {
                ars.Set();
                ars2.WaitOne(timeout, true);
                if (qlist.Count == 0 && timeout != Timeout.Infinite)
                {
                    //若为超时
                    return null;
                }
                ars.WaitOne(Timeout.Infinite, true);
            }
            List<T> ret = new List<T>();
            ret.AddRange(qlist);

            qlist.Clear();
            ars.Set();
            return ret;
        }

        /// <summary>
        /// 取得所有元素，如果没内容则阻塞
        /// </summary>
        /// <returns></returns>
        public List<T> PeekAll()
        {
            ars.WaitOne(Timeout.Infinite, true);
            while (qlist.Count == 0)
            {
                ars.Set();
                ars2.WaitOne(Timeout.Infinite, true);
                ars.WaitOne(Timeout.Infinite, true);
            }

            List<T> ret = new List<T>();
            ret.AddRange(qlist);

            ars.Set();
            return ret;
        }

        /// <summary>
        /// 删除元素t
        /// </summary>
        /// <param name="t"></param>
        public void RemoveItem(T t)
        {
            ars.WaitOne(Timeout.Infinite, true);
            qlist.Remove(t);
            ars.Set();
        }
    }
}
