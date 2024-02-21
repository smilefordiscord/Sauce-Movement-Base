using Sandbox;
using Sandbox.Citizen;

public sealed class PlayerController : Component
{
	[Property] private float Sensitivity = 1.5f;

    // Movement Properties
    [Property] public float Weight {get;set;} =  1f;
    [Property] public float MoveSpeed {get;set;} = 250f;
    [Property] public float ShiftSpeed {get;set;} = 130f;
    [Property] public float CrouchSpeed {get;set;} = 85f;
    [Property] public float StopSpeed {get;set;} = 80f;
    [Property] public float Friction {get;set;} = 5.2f;
    [Property] public float Acceleration {get;set;} = 5.5f;
    [Property] public float AirAcceleration {get;set;} = 12f;
    [Property] public float MaxAirWishSpeed {get;set;} = 30f;
    [Property] public float JumpForce {get;set;} = 301.993378f;
    [Property] private bool HoldToJump {get;set;} = false;
    [Property] public Vector3 Gravity {get;set;} = new Vector3(0, 0, -800f);
    [Property] public float MaxStamina {get;set;} = 80f;
    [Property] public float StaminaRecoveryRate {get;set;} = 60f;
    [Property] public float StaminaJumpCost {get;set;} =  0.08f;
    [Property] public float StaminaLandingCost {get;set;} =  0.05f;

    // Object References
    [Property] public GameObject Head {get;set;}
    [Property] public GameObject Body {get;set;}
    public GameObject UI;

    // // Member Variables
    [Sync] public bool IsCrouching {get;set;} = false;
    public bool IsWalking = false;
    private CharacterController characterController;
    private CitizenAnimationHelper animationHelper;
    
    public float Stamina = 80f;
    [Sync] private float InternalMoveSpeed {get;set;} = 250f;
    private bool AlreadyGrounded = true;
    private float CrouchTime = 0.1f;
    private float jumpStartHeight = 0f;
    private float jumpMaxHeight = 0f;

    [Sync] private Vector3 WishDir {get;set;} = Vector3.Zero;
    
	private CameraComponent Camera;
	private ModelRenderer BodyRenderer;
	[Sync] public Vector2 LookAngle {get;set;}
    
	protected override void OnAwake() {
        Scene.FixedUpdateFrequency = 64;

        BodyRenderer = Body.Components.Get<ModelRenderer>();
        animationHelper = Components.GetInChildrenOrSelf<CitizenAnimationHelper>();
        characterController = Components.Get<CharacterController>();

		if ( IsProxy )
			return;
        
        // UI.Name = "LocalUI";
        // UI.Components.Create<ScreenPanel>();
        // UI.Components.Create<TestUI>();

        characterController.Radius = 16;
        characterController.Height = 72;
        characterController.Bounciness = 0;
        characterController.Acceleration = 0;
        
		Camera = Scene.Camera.Components.Get<CameraComponent>();
        
    }

    Angles GetLookAngleAsAngles() {
        return new Angles(LookAngle.x, LookAngle.y, 0);
    }

    float GetStaminaMultiplier() {
        return Stamina / MaxStamina;
    }

    bool IsOnSlope() {
        // float angle = characterController.GroundCollider.Transform.Rotation.Angles().AsVector3().z;
        // if (angle < characterController.GroundAngle && angle != angle) {
        //     return true;
        // }
        if (characterController.GroundCollider == null) return false;
        // float angle = Vector3.GetAngle(Vector3.Up, characterController.GroundCollider.Transform.Rotation.Angles().AsVector3());
        // Log.Info(angle);
        return false;
    }

    void GatherInput() {
        WishDir = 0;

        var rot = new Angles(0, Head.Transform.Rotation.Angles().yaw, 0).ToRotation();
        if (Input.Down("Forward")) WishDir += rot.Forward;
        if (Input.Down("Backward")) WishDir += rot.Backward;
        if (Input.Down("Left")) WishDir += rot.Left;
        if (Input.Down("Right")) WishDir += rot.Right;
        
        WishDir = WishDir.WithZ( 0 );

        if ( !WishDir.IsNearZeroLength ) WishDir = WishDir.Normal;

        IsWalking = Input.Down("Slow");
        IsCrouching = Input.Down("Duck");
        if (Input.Pressed("Duck") || Input.Released("Duck")) CrouchTime += 0.1f;
    }

    void ApplyFriction() {
        float speed, newspeed, control, drop;

        speed = characterController.Velocity.Length;

        // If too slow, return
        if (speed < 0.1f) return;

        drop = 0;

        // Apply ground friction
        if (characterController.IsOnGround)
        {
            // Bleed off some speed, but if we have less than the bleed
            // threshold, bleed the threshold amount.
            if (speed < StopSpeed) {
                control = StopSpeed;
            } else {
                control = speed;
            }
            // Add the amount to the drop amount.
            drop += control * Friction * Time.Delta;
        }

        // Scale the velocity
        newspeed = speed - drop;
        if (newspeed < 0) newspeed = 0;

        if (newspeed != speed)
        {
            // Determine proportion of old speed we are using.
            newspeed /= speed;
            // Adjust velocity according to proportion.
            characterController.Velocity *= newspeed;
        }

        characterController.Velocity -= (1 - newspeed) * characterController.Velocity;
    }

    void Accelerate(Vector3 wishdir, float wishspeed, float accel) {
        // See if we are changing direction a bit
        float currentspeed = Vector3.Dot(characterController.Velocity, wishdir);

        // Reduce wishspeed by the amount of veer.
        float addspeed = wishspeed - currentspeed;

        // If not going to add any speed, done.
        if (addspeed <= 0) return;

        float accelspeed = accel * wishspeed * Time.Delta;

        if (accelspeed > addspeed) accelspeed = addspeed;
        
        characterController.Velocity += wishdir.WithZ(0) * accelspeed;
    }

    void AirAccelerate(Vector3 wishDir, float wishSpeed, float accel) {
        float addspeed, accelspeed, currentspeed;

        // Cap Speed 
        if (wishSpeed > MaxAirWishSpeed) {
            wishSpeed = MaxAirWishSpeed;
        }

        currentspeed = characterController.Velocity.Dot(wishDir);
        addspeed = wishSpeed - currentspeed;

        if (addspeed <= 0) {
            return;
        }

        accelspeed = accel * wishSpeed * Time.Delta;

        if (accelspeed > addspeed) {
            accelspeed = addspeed;
        }

        characterController.Velocity += wishDir * addspeed;
    }

    void GroundMove() {
        float wishSpeed = WishDir.Length * InternalMoveSpeed * 1.8135f; // this is shit, why mult???
        Accelerate(WishDir, wishSpeed, Acceleration);
        if (characterController.Velocity.z < 0) {
            characterController.Velocity = characterController.Velocity.WithZ(0);
        }

        if ((HoldToJump && Input.Down("Jump")) || Input.Pressed("Jump")) {
            characterController.Punch(new Vector3(0, 0, JumpForce * GetStaminaMultiplier()));
            Stamina -= Stamina * StaminaJumpCost * 2.9625f;
            Stamina = (Stamina * 10).FloorToInt() * 0.1f;
            if (Stamina < 0) Stamina = 0;
        }
        // else {
        //     characterController.Velocity = characterController.Velocity.WithZ(0);
        // }
    }

    void AirMove() {
        AirAccelerate(WishDir, WishDir.Length * InternalMoveSpeed, AirAcceleration);
    }

    void UpdateCitizenAnims() {
        if (animationHelper == null) return;

        animationHelper.WithWishVelocity(WishDir * InternalMoveSpeed);
        animationHelper.WithVelocity(characterController.Velocity);
        animationHelper.AimAngle = Head.Transform.Rotation;
        animationHelper.IsGrounded = characterController.IsOnGround;
        animationHelper.WithLook(Head.Transform.Rotation.Forward, 1f, 0.75f, 0.5f);
        animationHelper.MoveStyle = CitizenAnimationHelper.MoveStyles.Auto;
        animationHelper.DuckLevel = IsCrouching ? 1f : 0f;
    }

    protected override void OnFixedUpdate() {
		if ( IsProxy )
			return;
        
        GatherInput();
        
        InternalMoveSpeed = MoveSpeed;
        if (IsWalking) InternalMoveSpeed = ShiftSpeed;
        if (IsCrouching) InternalMoveSpeed = CrouchSpeed;
        InternalMoveSpeed *= GetStaminaMultiplier();
        InternalMoveSpeed *= Weight;

        var HeightGoal = 72;
        if (IsCrouching) HeightGoal = 54;
        CrouchTime = 0.125f; // TODO: FIX 3RD PERSON CROUCHING
        characterController.Height = characterController.Height.LerpTo(HeightGoal, Time.Delta / CrouchTime.Clamp(0.125f, 0.5f));
        Head.Transform.LocalPosition = new Vector3(0, 0, characterController.Height * 0.89f);

        characterController.Velocity += Gravity * Time.Delta * 0.5f;
        
        if(characterController.IsOnGround) ApplyFriction();

        if (AlreadyGrounded != characterController.IsOnGround) {
            if (characterController.IsOnGround) {
                var heightMult = (jumpMaxHeight - jumpStartHeight) / 46f;
                Stamina -= Stamina * StaminaLandingCost * 2.9625f * heightMult;
                Stamina = (Stamina * 10).FloorToInt() * 0.1f;
                if (Stamina < 0) Stamina = 0;
            } else {
                jumpStartHeight = GameObject.Transform.Position.z;
                jumpMaxHeight = GameObject.Transform.Position.z;
            }
            AlreadyGrounded = characterController.IsOnGround;
        }

        if(characterController.IsOnGround) {
            GroundMove();
            IsOnSlope();
            Camera.Components.Get<TestUI>().Speed = characterController.Velocity.Length.CeilToInt();
        } else {
            AirMove();
            Camera.Components.Get<TestUI>().Speed = characterController.Velocity.WithZ(0).Length.CeilToInt();
        }

        CrouchTime -= Time.Delta * 0.33f;
        CrouchTime = CrouchTime.Clamp(0f, 0.5f);

        Stamina += StaminaRecoveryRate * Time.Delta;
        if (Stamina > MaxStamina) Stamina = MaxStamina;

        var fovGoal = 90f + (30 * ((characterController.Velocity.WithZ(0).Length - 250) / 250).Clamp(0, 1));
        Camera.FieldOfView = Camera.FieldOfView.LerpTo(fovGoal, Time.Delta / 0.25f);
        
        // Log.Info(characterController.Velocity);
        characterController.Move();

        characterController.Velocity += Gravity * Time.Delta * 0.5f;

        if (jumpMaxHeight < GameObject.Transform.Position.z) jumpMaxHeight = GameObject.Transform.Position.z;
    }

	protected override void OnUpdate() {
        UpdateCitizenAnims();
        
		BodyRenderer.RenderType = ModelRenderer.ShadowRenderType.On;

        var Rotation = GetLookAngleAsAngles();
		Body.Transform.Rotation = Rotation.WithPitch(0).ToRotation();

        Rotation = GetLookAngleAsAngles().ToRotation();
		Head.Transform.Rotation = Rotation;
        
		if ( IsProxy )
			return;
        
		BodyRenderer.RenderType = ModelRenderer.ShadowRenderType.ShadowsOnly;

        LookAngle += new Vector2(Input.MouseDelta.y * Sensitivity * 0.022f, -Input.MouseDelta.x * Sensitivity * 0.022f);
        LookAngle = LookAngle.WithX(LookAngle.x.Clamp(-89f, 89f));
		
		Camera.Transform.Position = Head.Transform.Position;
		Camera.Transform.Rotation = Head.Transform.Rotation;
	}

}