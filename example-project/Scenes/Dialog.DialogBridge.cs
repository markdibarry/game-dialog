using System.Threading.Tasks;
using Godot;

namespace ExampleProject;

public partial class Dialog
{
    public void Flash()
    {
        _ = FlashAsync();
        
        async ValueTask FlashAsync()
        {
            Node2D testScene = Game.Root.TestScene;
            Tween tween = GetTree().CreateTween();
            tween.TweenProperty(testScene, "modulate", new Color(20, 20, 20, 1), 0.50f)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.InOut);
            tween.TweenProperty(testScene, "modulate", Colors.White, 0.50f)
              .SetTrans(Tween.TransitionType.Sine)
              .SetEase(Tween.EaseType.InOut)
              .SetDelay(0.25f);

            await ToSignal(tween, "finished");
            Resume();
        }
    }
}