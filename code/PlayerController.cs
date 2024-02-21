using Sandbox;
using Sandbox.Citizen;

public sealed class PlayerController : Component
{
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
    
    // Stamina Properties
    [Property] public float MaxStamina {get;set;} = 80f;
    [Property] public float StaminaRecoveryRate {get;set;} = 60f;
    [Property] public float StaminaJumpCost {get;set;} =  0.08f;
    [Property] public float StaminaLandingCost {get;set;} =  0.05f;

    // Other Properties
    [Property] public TagSet IgnoreLayers { get; set; } = new TagSet();
    [Property] public GameObject Head {get;set;}
    [Property] public GameObject Body {get;set;}

    // State Bools
    [Sync] public bool IsCrouching {get;set;} = false;
    public bool IsWalking = false;
    [Sync] public bool IsOnGround {get;set;} = false;

    // Internal objects
    private GameObject UI;
    private CitizenAnimationHelper animationHelper;
	private CameraComponent Camera;
	private ModelRenderer BodyRenderer;

    // Internal Variables
    public float Stamina = 80f;
    private float CrouchTime = 0.1f;
    private float jumpStartHeight = 0f;
    private float jumpHighestHeight = 0f;
    private bool AlreadyGrounded = true;

    // Size
    private float Radius = 16f;
    private float Height = 72f;
    private BBox BoundingBox => new BBox(new Vector3(0f - Radius, 0f - Radius, 0f), new Vector3(Radius, Radius, Height));
    private int _stuckTries;

    // Synced internal vars
    [Sync] private float InternalMoveSpeed {get;set;} = 250f;
    [Sync] public Vector3 WishDir {get;set;} = Vector3.Zero;
    [Sync] public Vector3 Velocity {get;set;} = Vector3.Zero;
	[Sync] public Vector2 LookAngle {get;set;}
    
    // Fucntions to make things slightly nicer

    Angles GetLookAngleAsAngles() {
        return new Angles(LookAngle.x, LookAngle.y, 0);
    }

    float GetStaminaMultiplier() {
        return Stamina / MaxStamina;
    }

    public void Punch(in Vector3 amount) {
        ClearGround();
        Velocity += amount;
    }

    private void ClearGround() {
        IsOnGround = false;
    }

    // Character Controller Functions
    
    private void Move(bool step) {
        if (step && IsOnGround)
        {
            Velocity = Velocity.WithZ(0f);
        }

        if (Velocity.Length < 0.001f)
        {
            Velocity = Vector3.Zero;
            return;
        }

        Vector3 position = base.GameObject.Transform.Position;
        CharacterControllerHelper characterControllerHelper = new CharacterControllerHelper(BuildTrace(position, position), position, Velocity);
        characterControllerHelper.Bounce = 0;
        characterControllerHelper.MaxStandableAngle = 45.5f;
        if (step && IsOnGround)
        {
            characterControllerHelper.TryMoveWithStep(Time.Delta, 18);
        }
        else
        {
            characterControllerHelper.TryMove(Time.Delta);
        }

        base.Transform.Position = characterControllerHelper.Position;
        Velocity = characterControllerHelper.Velocity;
    }
    
    private void Move()
    {
        if (!TryUnstuck())
        {
            if (IsOnGround)
            {
                Move(step: true);
            }
            else
            {
                Move(step: false);
            }

            CategorizePosition();
        }
    }

    private bool TryUnstuck() {
        if (!BuildTrace(base.Transform.Position, base.Transform.Position).Run().StartedSolid)
        {
            _stuckTries = 0;
            return false;
        }

        int num = 20;
        for (int i = 0; i < num; i++)
        {
            Vector3 vector = base.Transform.Position + Vector3.Random.Normal * ((float)_stuckTries / 2f);
            if (i == 0)
            {
                vector = base.Transform.Position + Vector3.Up * 2f;
            }

            if (!BuildTrace(vector, vector).Run().StartedSolid)
            {
                base.Transform.Position = vector;
                return false;
            }
        }

        _stuckTries++;
        return true;
    }

    private void CategorizePosition() {
        Vector3 position = base.Transform.Position;
        Vector3 to = position + Vector3.Down * 2f;
        Vector3 from = position;
        bool isOnGround = IsOnGround;
        if (!IsOnGround && Velocity.z > 40f)
        {
            ClearGround();
            return;
        }

        to.z -= (isOnGround ? 18 : 0.1f);
        SceneTraceResult sceneTraceResult = BuildTrace(from, to).Run();
        if (!sceneTraceResult.Hit || Vector3.GetAngle(in Vector3.Up, in sceneTraceResult.Normal) > 45.5)
        {
            ClearGround();
            return;
        }

        IsOnGround = true;
        // GroundObject = sceneTraceResult.GameObject;
        // GroundCollider = sceneTraceResult.Shape?.Collider as Collider;
        if (isOnGround && !sceneTraceResult.StartedSolid && sceneTraceResult.Fraction > 0f && sceneTraceResult.Fraction < 1f)
        {
            base.Transform.Position = sceneTraceResult.EndPosition + sceneTraceResult.Normal * 0.01f;
        }
    }

    private SceneTrace BuildTrace(Vector3 from, Vector3 to) {
        return BuildTrace(base.Scene.Trace.Ray(in from, in to));
    }

    private SceneTrace BuildTrace(SceneTrace source) {
        BBox hull = BoundingBox;
        return source.Size(in hull).WithoutTags(IgnoreLayers).IgnoreGameObjectHierarchy(base.GameObject);
    }

    private void GatherInput() {
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

    private void UpdateCitizenAnims() {
        if (animationHelper == null) return;

        animationHelper.WithWishVelocity(WishDir * InternalMoveSpeed);
        animationHelper.WithVelocity(Velocity);
        animationHelper.AimAngle = Head.Transform.Rotation;
        animationHelper.IsGrounded = IsOnGround;
        animationHelper.WithLook(Head.Transform.Rotation.Forward, 1f, 0.75f, 0.5f);
        animationHelper.MoveStyle = CitizenAnimationHelper.MoveStyles.Auto;
        animationHelper.DuckLevel = IsCrouching ? 1f : 0f;
    }

    // Source engine magic functions

    private void ApplyFriction() {
        float speed, newspeed, control, drop;

        speed = Velocity.Length;

        // If too slow, return
        if (speed < 0.1f) return;

        drop = 0;

        // Apply ground friction
        if (IsOnGround)
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
            Velocity *= newspeed;
        }

        Velocity -= (1 - newspeed) * Velocity;
    }

    private void Accelerate(Vector3 wishdir, float wishspeed, float accel) {
        // See if we are changing direction a bit
        float currentspeed = Vector3.Dot(Velocity, wishdir);

        // Reduce wishspeed by the amount of veer.
        float addspeed = wishspeed - currentspeed;

        // If not going to add any speed, done.
        if (addspeed <= 0) return;

        float accelspeed = accel * wishspeed * Time.Delta;

        if (accelspeed > addspeed) accelspeed = addspeed;
        
        Velocity += wishdir.WithZ(0) * accelspeed;
    }

    private void AirAccelerate(Vector3 wishDir, float wishSpeed, float accel) {
        float addspeed, accelspeed, currentspeed;

        // Cap Speed 
        if (wishSpeed > MaxAirWishSpeed) {
            wishSpeed = MaxAirWishSpeed;
        }

        currentspeed = Velocity.Dot(wishDir);
        addspeed = wishSpeed - currentspeed;

        if (addspeed <= 0) {
            return;
        }

        accelspeed = accel * wishSpeed * Time.Delta;

        if (accelspeed > addspeed) {
            accelspeed = addspeed;
        }

        Velocity += wishDir * addspeed;
    }
    
    private void GroundMove() {
        float wishSpeed = WishDir.Length * InternalMoveSpeed * 1.8135f; // this is shit, why mult???
        Accelerate(WishDir, wishSpeed, Acceleration);
        if (Velocity.z < 0) Velocity = Velocity.WithZ(0);

        if ((HoldToJump && Input.Down("Jump")) || Input.Pressed("Jump")) {
            Punch(new Vector3(0, 0, JumpForce * GetStaminaMultiplier()));
            Stamina -= Stamina * StaminaJumpCost * 2.9625f;
            Stamina = (Stamina * 10).FloorToInt() * 0.1f;
            if (Stamina < 0) Stamina = 0;
        }
    }

    private void AirMove() {
        AirAccelerate(WishDir, WishDir.Length * InternalMoveSpeed, AirAcceleration);
    }
    
    // Overrides

	protected override void OnAwake() {
        Scene.FixedUpdateFrequency = 64;

        BodyRenderer = Body.Components.Get<ModelRenderer>();
        animationHelper = Components.GetInChildrenOrSelf<CitizenAnimationHelper>();

		if ( IsProxy )
			return;
        
		Camera = Scene.Camera.Components.Get<CameraComponent>();
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
        Height = Height.LerpTo(HeightGoal, Time.Delta / CrouchTime.Clamp(0.125f, 0.5f));
        Head.Transform.LocalPosition = new Vector3(0, 0, Height * 0.89f);

        Velocity += Gravity * Time.Delta * 0.5f;
        
        if(IsOnGround) ApplyFriction();

        if (AlreadyGrounded != IsOnGround) {
            if (IsOnGround) {
                var heightMult = (jumpHighestHeight - jumpStartHeight) / 46f;
                Stamina -= Stamina * StaminaLandingCost * 2.9625f * heightMult;
                Stamina = (Stamina * 10).FloorToInt() * 0.1f;
                if (Stamina < 0) Stamina = 0;
            } else {
                jumpStartHeight = GameObject.Transform.Position.z;
                jumpHighestHeight = GameObject.Transform.Position.z;
            }
            AlreadyGrounded = IsOnGround;
        }

        if(IsOnGround) {
            GroundMove();
            // IsOnSlope();
            Camera.Components.Get<TestUI>().Speed = Velocity.Length.CeilToInt();
        } else {
            AirMove();
            Camera.Components.Get<TestUI>().Speed = Velocity.WithZ(0).Length.CeilToInt();
        }

        CrouchTime -= Time.Delta * 0.33f;
        CrouchTime = CrouchTime.Clamp(0f, 0.5f);

        Stamina += StaminaRecoveryRate * Time.Delta;
        if (Stamina > MaxStamina) Stamina = MaxStamina;
        
        var fovGoal = 100f + (20 * ((Velocity.WithZ(0).Length - 250) / 250).Clamp(0, 1));
        Camera.FieldOfView = Camera.FieldOfView.LerpTo(fovGoal, Time.Delta / 0.25f);
        
        // Log.Info(Velocity);
        if (Velocity.Length != 0) {
            Move();
        }

        Velocity += Gravity * Time.Delta * 0.5f;

        if (jumpHighestHeight < GameObject.Transform.Position.z) jumpHighestHeight = GameObject.Transform.Position.z;
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

        LookAngle += new Vector2(Input.MouseDelta.y * Preferences.Sensitivity * 0.022f, -Input.MouseDelta.x * Preferences.Sensitivity * 0.022f);
        LookAngle = LookAngle.WithX(LookAngle.x.Clamp(-89f, 89f));
		
		Camera.Transform.Position = Head.Transform.Position;
		Camera.Transform.Rotation = Head.Transform.Rotation;
	}

}