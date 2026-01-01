using Godot;

namespace ExampleProject;

public partial class Game : Node2D
{
    public static Game Root { get; private set; } = null!;
    public Node2D TestScene { get; set; } = null!;
    public CanvasLayer GUI { get; set; } = null!;

    public override void _Ready()
    {
        Root = this;
        TestScene = GetNode<Node2D>("TestScene");
        GUI = GetNode<CanvasLayer>("GUI");
    }
}
