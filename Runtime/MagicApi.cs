using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace Polaris.Magic.Runtime
{
    /// <summary>
    /// <c>context.Magic</c>：这次施法能创建的中间层资源。
    ///
    /// 创建出来的对象归本次施法所有，Task 结束时自动清理。取消之后不再允许创建新的世界对象——
    /// 那时候只应该做幂等清理，新建对象只会变成没人负责释放的残留。
    /// </summary>
    public sealed class MagicApi
    {
        private readonly Assembly owner;
        private readonly string instanceLabel;
        private readonly List<MagicObject> objects = new List<MagicObject>();

        private bool closed;
        private int nextObjectIndex;

        internal MagicApi(Assembly owner, string instanceLabel)
        {
            this.owner = owner;
            this.instanceLabel = instanceLabel;
        }

        /// <summary>创建一个新的魔法对象。</summary>
        public MagicObject CreateObject()
        {
            if (closed)
            {
                throw new InvalidOperationException(
                    "This magic instance has already ended; only idempotent cleanup is allowed from here on.");
            }

            string name = instanceLabel + "#" + nextObjectIndex.ToString(CultureInfo.InvariantCulture);
            nextObjectIndex++;

            var created = new MagicObject(owner, name);
            objects.Add(created);
            return created;
        }

        internal void Tick(MagicClock clock)
        {
            for (int i = objects.Count - 1; i >= 0; i--)
            {
                MagicObject item = objects[i];
                if (item.IsDisposed)
                {
                    objects.RemoveAt(i);
                    continue;
                }

                item.Tick(clock);
            }
        }

        /// <summary>
        /// 关闭创建入口并释放全部未释放的对象。由实例在 Task 彻底结束后调用一次；
        /// 作者自己已经 <c>Dispose</c> 过的对象在这里是空操作。
        /// </summary>
        internal void Close()
        {
            closed = true;

            MagicObject[] snapshot = objects.ToArray();
            objects.Clear();
            foreach (MagicObject item in snapshot)
            {
                item.Dispose();
            }
        }
    }
}
