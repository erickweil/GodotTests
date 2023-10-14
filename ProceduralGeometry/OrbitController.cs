using Godot;
using System;

// Just to provide a way to orbit around with the mouse
// MUST HAVE a Camera3D child
public partial class OrbitController : Node3D
{
	public float sensitivity = 0.25f;

	public float height = 0.0f;

	const float backMaxDist = 5.0f;
	const float backMinDist = 0.005f;
	private float backDist = 0.5f;
	public float targetBackDist;

	public Camera3D camera;
	private float rotationHorizontal = 0.0f;
	private float rotationVertical = -30.0f;


	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		targetBackDist = backDist;

		// Para não aparecer o mouse
		Input.MouseMode = Input.MouseModeEnum.Captured;
		camera = GetNode<Camera3D>("Camera3D");
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// Verificar se o mouse moveu
		if (@event is InputEventMouseMotion keyEventMotion) {
			Vector2 mouseOffset = keyEventMotion.Relative;
			rotationHorizontal -= mouseOffset.X * sensitivity;
			rotationVertical -= mouseOffset.Y * sensitivity;

			// Limitar a rotação vertical entre -90 e 90 graus
			rotationVertical = Mathf.Clamp(rotationVertical, -89.0f, 89.0f);
			// Para não ficar um valor muito grande e causar problemas de ponto flutuante
			rotationHorizontal = Mathf.Wrap(rotationHorizontal, 0.0f, 360.0f);

			//rotationChanged = true;
		} else if (@event is InputEventMouseButton keyEventButton) {
			switch (keyEventButton.ButtonIndex)
			{
				case MouseButton.WheelUp:
					targetBackDist -= targetBackDist * 0.05f;
					targetBackDist = Mathf.Clamp(targetBackDist, backMinDist, backMaxDist);
					
					break;
				case MouseButton.WheelDown:
					targetBackDist += targetBackDist * 0.05f;
					targetBackDist = Mathf.Clamp(targetBackDist, backMinDist, backMaxDist);
					
					break;
			}

			//rotationChanged = true;
		}
	}

	public void updateCamera3D(double delta) {
		// 1. Rotacionar o Pivot no eixo Y de acordo com a movimentação horizontal
		this.Rotation = new Vector3(0, Mathf.DegToRad(rotationHorizontal), 0);

		backDist = Mathf.Lerp(backDist, targetBackDist, 0.25f);
		// 2. Rotacionar a Câmera ao redor do Pivot no eixo X de acordo com a movimentação vertical
		// calculando manualmente posição e rotação local da câmera
		Vector3 local = new Vector3(0, 0, -backDist);
		float sin = Mathf.Sin(-rotationVertical * (Mathf.Pi / 180.0f));
		float cos = Mathf.Cos(-rotationVertical * (Mathf.Pi / 180.0f));
		Vector3 rotLocal = new Vector3(
			0.0f,
			local.Y * cos - local.Z * sin,
			local.Y * sin - local.Z * cos
		);

		camera.Position = rotLocal + new Vector3(0, height, 0);
		camera.Rotation = new Vector3(Mathf.DegToRad(rotationVertical), 0, 0);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if(!Input.IsPhysicalKeyPressed(Key.G)) {
			updateCamera3D(delta);
		}

		if(Input.IsPhysicalKeyPressed(Key.Escape)) {
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}
	}
}
