using System.Threading.Tasks;
using Godot;

namespace ExampleProject;

public partial class Dialog
{
    public void Shake()
    {
        if (DialogBox == null)
            return;

        _ = ShakeAsync();

        async ValueTask ShakeAsync()
        {
            Vector2 original = DialogBox!.Position;
            float shakeAmt = 5f;
            float totalTime = 1.5f;
            int loops = 15;
            float halfCycle = totalTime / (loops * 2);

            Tween tween = DialogBox.CreateTween();
            tween.TweenProperty(DialogBox, "position:x", original.X + shakeAmt, halfCycle)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.InOut);
            tween.TweenProperty(DialogBox, "position:x", original.X, halfCycle)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.InOut);
            tween.SetLoops(loops);
            await ToSignal(tween, "finished");
            Resume();
        }
    }

    public void Flash()
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
    }

    public float GetTimesTalked(string nodeName)
    {
        DialogArea? dialogArea = Game.Root.FindChild(nodeName) as DialogArea;
        return dialogArea?.TimesTalked ?? 0;
    }
}