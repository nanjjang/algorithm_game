using Wizard.Solo.Sim;

namespace Wizard.Solo.App
{
    /// <summary>실행기(SessionCore)가 표시 쪽에 하는 말. 실물은 WorldViewHost, 테스트는 가짜(또는 null).</summary>
    public interface IWorldViewHost
    {
        /// <summary>표시 층을 세운다(진행 중인 세계여도 된다 — 보조 판에서 돌아올 때). 앞의 층이 있으면 먼저 걷는다.</summary>
        void Build(SimWorld w);
        void OnEvent(SimWorld w, in SimEvent e);
        /// <summary>프레임마다. w = null이면 판이 없다(카메라만 맞춘다). dt = 프레임 초(일시정지 중 0).</summary>
        void Render(SimWorld w, float alpha, float dt);
        /// <summary>표시 층만 걷는다(카메라·캔버스는 남는다).</summary>
        void DisposeLayers();
    }
}
