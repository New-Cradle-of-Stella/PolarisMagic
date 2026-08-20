using nel;

namespace Polaris.Magic.Game
{
    /// <summary>
    /// 一种自定义魔法在<b>一个</b> <c>MGContainer</c> 里的 holder。
    ///
    /// 每个容器一个实例，不做全局单例：holder 持有容器引用，而原版 <c>MGContainer.destruct</c> 会挨个
    /// 调它字典里 holder 的 <c>destruct</c>——共享一个实例的话，先销毁的那张地图会把还活着的另一张
    /// 地图的状态一起清掉。
    ///
    /// 正式态由中间层完全接管：原版只负责提供和回收 <c>MagicItem</c> 容器，不参与运行状态。
    /// </summary>
    internal sealed class MagicTaskHolder : MgFDHolder
    {
        private readonly MagicRegistration registration;
        private readonly MGContainer container;

        internal MagicTaskHolder(MagicRegistration registration, MGContainer container)
        {
            this.registration = registration;
            this.container = container;
        }

        internal MagicRegistration Registration => registration;

        public override bool run(MagicItem Mg, float fcnt) => MagicRuntimeHost.Run(registration, Mg, fcnt);

        /// <summary>
        /// 原版绘制通道不使用：表现全部走 PolarisCore 的 Drawing（Map 空间、地图生命周期），
        /// 这样作者的图片和特效在切图时一定被回收，也不必自己接 <c>M2DrawBinder</c>。
        /// </summary>
        public override bool draw(MagicItem Mg, float fcnt) => true;

        /// <summary>
        /// 只装运行委托、不装绘制委托：绘制委托非空会让 <c>MagicItem</c> 去建一个原版 <c>M2DrawBinder</c>，
        /// 而我们根本不用它。
        /// </summary>
        public override MagicItem initFunc(MagicItem Mg)
        {
            Mg.initFunc(FD_Run);
            return Mg;
        }

        public override MagicNotifiear GetNotifiear() => registration.NotifierTemplate;

        public override void destruct()
        {
            MagicRuntimeHost.DropContainer(container);
            base.destruct();
        }
    }
}
