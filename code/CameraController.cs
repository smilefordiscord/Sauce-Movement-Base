using System.Reflection.Emit;
using Sandbox;

public sealed class CameraController : Component
{
	[Property] public float Sensitivity = 1.5f;
    [Property] public GameObject Head { get; set; }
    [Property] public GameObject Body { get; set; }

	private CameraComponent Camera;
	private ModelRenderer BodyRenderer;
	private Angles LookAngle;

	protected override void OnAwake() {
		if ( IsProxy )
			return;
		
		Camera = Components.Create<CameraComponent>();
		Camera.IsMainCamera = true;
		BodyRenderer = Body.Components.Get<ModelRenderer>();
		BodyRenderer.RenderType = ModelRenderer.ShadowRenderType.ShadowsOnly;
	}

	protected override void OnUpdate() {
		if ( IsProxy )
			return;
		
		// var EyeAngles = Head.Transform.Rotation.Angles();
		LookAngle.pitch += Input.MouseDelta.y * Sensitivity * 0.022f;
		LookAngle.pitch = LookAngle.pitch.Clamp(-89f, 89f);
		LookAngle.yaw -= Input.MouseDelta.x * Sensitivity * 0.022f;
		LookAngle.roll = 0;
		var BodyAngles = new Angles();
		BodyAngles.yaw = LookAngle.yaw;
		Body.Transform.Rotation = BodyAngles.ToRotation();
		Head.Transform.Rotation = LookAngle.ToRotation();
		Camera.Transform.Position = Head.Transform.Position;
		Camera.Transform.Rotation = Head.Transform.Rotation;
	}
}