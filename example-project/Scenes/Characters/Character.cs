using Godot;

namespace ExampleProject;

public partial class Character : CharacterBody2D
{
    public Sprite2D Sprite2D { get; set; } = default!;
    public AnimationPlayer AnimationPlayer { get; set; } = default!;
    public DialogArea? DialogArea { get; set; }
    public const float Speed = 150.0f;
    public const float JumpVelocity = -250.0f;

    public override void _Ready()
    {
        Sprite2D = GetNode<Sprite2D>("Sprite2D");
        AnimationPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector2 velocity = Velocity;

        if (!IsOnFloor())
        {
            velocity += GetGravity() * (float)delta;
        }

        if (Input.IsActionJustPressed("ui_accept") && IsOnFloor())
        {
            if (DialogArea != null)
            {
                DialogArea.RunDialog();
                return;
            }

            velocity.Y = JumpVelocity;
        }

        Vector2 direction = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
        if (direction != Vector2.Zero)
        {
            Sprite2D.FlipH = direction.X < 0;
            AnimationPlayer.Play("Walk");
            velocity.X = direction.X * Speed;
        }
        else
        {
            AnimationPlayer.Play("Idle");
            velocity.X = Mathf.MoveToward(Velocity.X, 0, Speed);
        }

        Velocity = velocity;
        MoveAndSlide();
    }
}
