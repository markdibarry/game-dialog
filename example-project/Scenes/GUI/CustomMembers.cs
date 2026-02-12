using System.Threading.Tasks;
using GameDialog.Runner;
using Godot;

namespace ExampleProject;

[DialogBridge]
public partial class CustomMembers
{
    public void Flash()
    {
        Node2D testScene = Game.Root.TestScene;
        Tween tween = testScene.GetTree().CreateTween();
        tween.TweenProperty(testScene, "modulate", new Color(20, 20, 20, 1), 0.50f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(testScene, "modulate", Colors.White, 0.50f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut)
            .SetDelay(0.25f);
    }

    public async ValueTask Shake()
    {
        if (Dialog.Context is not DialogBox dialogBox)
            return;

        Vector2 original = dialogBox!.Position;
        float shakeAmt = 5f;
        float totalTime = 1.5f;
        int loops = 15;
        float halfCycle = totalTime / (loops * 2);

        Tween tween = dialogBox.CreateTween();
        tween.TweenProperty(dialogBox, "position:x", original.X + shakeAmt, halfCycle)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(dialogBox, "position:x", original.X, halfCycle)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        tween.SetLoops(loops);
        await dialogBox.ToSignal(tween, "finished");
    }

    public float GetTimesTalked(string nodeName)
    {
        DialogArea? dialogArea = Game.Root.FindChild(nodeName) as DialogArea;
        return dialogArea?.TimesTalked ?? 0;
    }
}