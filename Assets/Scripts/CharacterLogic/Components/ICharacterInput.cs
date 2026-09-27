using ComponentLogic;
using UnityEngine;

namespace CharacterLogic
{
    public interface ICharacterInput : IComponent
    {
        bool IsReady { get; }
        Vector2 ReadMove();
        Vector2 ReadLook();
        bool JumpPressed();
        bool IsRunning();
        bool SitPressed();
        bool SitHeld();
        bool AttackPressed();
        bool BlinkPressed();
        bool ParkourPressed();
        float ReadZoom();
    }
}
