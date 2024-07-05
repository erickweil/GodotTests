using Godot;
using System;

public partial class LineDrawing : Node3D
{
	[Export]
	MeshInstance3D meshInstance;

	LineDrawer drawer;

	float anim;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		drawer = new LineImmediateMesh(meshInstance);

		anim = 0;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		anim += (float)delta;
		// Rotate the vectors by anim
		Vector3 forward = new Vector3(0, 0, 1).Rotated(new Vector3(0, 1, 0), anim/3.0f);
		Vector3 right = new Vector3(1, 0, 0).Rotated(new Vector3(0, 1, 0), anim/2.0f);
		Vector3 up = new Vector3(0, 1, 0).Rotated(new Vector3(1, 0, 0), anim);


		drawer.Clear();

		drawer.DrawCircle(new Vector3(0, 0, 0), forward, new Vector3(0, 1, 0), Colors.Red, 128);
		drawer.DrawCircle(new Vector3(0, 0, 0), up, new Vector3(1, 0, 0), Colors.Green, 64);
		drawer.DrawCircle(new Vector3(0, 0, 0), right, new Vector3(0, 1, 0), Colors.Blue, 32);


		drawer.Render();
	}
}
