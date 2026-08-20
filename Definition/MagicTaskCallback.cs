using System.Threading;
using System.Threading.Tasks;
using Polaris.Magic.Runtime;

namespace Polaris.Magic.Definitions
{
    /// <summary>
    /// 作者回调。一次正式施法只调用一次，之后不再调用——魔法的整个生命周期就是这个 Task 的生命周期：
    /// Task 未完成时魔法继续运行，Task 完成、取消或异常时魔法立即结束。
    /// </summary>
    public delegate Task MagicTaskCallback(MagicRuntimeContext context, CancellationToken cancellationToken);
}
