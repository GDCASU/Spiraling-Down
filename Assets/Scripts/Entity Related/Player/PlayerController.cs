using System.Collections;
using UnityEngine;

/* -----------------------------------------------------------
 * Author:
 * Ian Fletcher
 * 
 * Modified By: 
 * William Peng, Jacob Kaufman-Warner
 */// --------------------------------------------------------

/* -----------------------------------------------------------
 * Pupose:
 * Handle the movement and input of the player
 */// --------------------------------------------------------


/// <summary>
/// Controls the player
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator moveController;
    [SerializeField] private SpriteRenderer playerRenderer;
    // Ian HACK: We probably shouldnt use the character controller for
    // this, so script the movement system ourselves later for more granular control
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private GameObject projectileDirArrow;
    
    [Header("Movement")]
    [SerializeField] private float _speedBeforeAcceleration;
    [SerializeField] private float _speedAfterAcceleration;
    [SerializeField] private float _delayBeforeAcceleration;
    [SerializeField] private float _durationOfAcceleration;
    
    [Header("Combat")]
    [SerializeField] private LayerMask _groundLayers;
    
    [Header("Audio")]
    [SerializeField] private MultiAudioEmitter _audioEmitter;
    private readonly int footstepsHash = AudioEmitterTools.StringToInteger("Footsteps");
    
    // Use this bool to gate all your Debug.Log Statements please
    [Header("Debugging")] 
    [SerializeField] private float cameraRotateSpeed;
    [SerializeField] private bool _doDebugLog;
    
    // Local Variables
    private PlayerAmmoManager ammoManager;
    private float _elapsedMovementTime = 0; // Time since movement in the current direction started
    private Vector3 _previousInputVector = Vector3.zero;
    private readonly int IsMoving = Animator.StringToHash("isMoving");
    private readonly int IsMovingForward = Animator.StringToHash("isMovingForward");
    private readonly int IsAttacking = Animator.StringToHash("isAttacking");
    
	private void Start()
	{
        // Get Components
        ammoManager = GetComponent<PlayerAmmoManager>();
        
        // Player HUD Binds
        InputManager.OnChangeElement += SwitchAmmoSlotHUD;
		InputManager.OnAttack += AttackAction;
        
        // Start the projectile dir orientation routine
        StartCoroutine(ProjectileDirRoutine());
    }

	// Update is called once per frame
	void Update()
    {
        // Get Vector2 Input from Input Manager
        Vector3 input = InputManager.Instance.movementInput;

        // Since the player could be slightly rotated from the
        // world axis, we compute the rotation
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        // Compute the actual world space direction of movement for the player
        Vector3 moveDirWorldSpace = (forward * input.y + right * input.x).normalized;

        // Set movement duration
        if (input == _previousInputVector) 
        {
            _elapsedMovementTime += Time.deltaTime;
        } 
        else 
        {
            _elapsedMovementTime = 0;
            _previousInputVector = input;
        }

        // Move Player
        float finalRelativeSpeedIncrease = (_speedAfterAcceleration / _speedBeforeAcceleration) - 1;
        float relativeSpeedIncrease = finalRelativeSpeedIncrease * Mathf.Clamp01((_elapsedMovementTime - _delayBeforeAcceleration) / _durationOfAcceleration);
        float speedMultiplier = 1 + relativeSpeedIncrease;
        _characterController.Move(Time.deltaTime * _speedBeforeAcceleration * speedMultiplier * moveDirWorldSpace);

        // Play walking animation if moving
        moveController.SetBool(IsMoving, Mathf.Abs(input.x) > 0 || Mathf.Abs(input.y) > 0);
        moveController.SetBool(IsMovingForward, input.y < 0);
        moveController.SetBool(IsAttacking, InputManager.Instance.isAttacking);
        
        // Check for attack strafing
        if (moveController.GetBool(IsMoving) && moveController.GetBool(IsAttacking))
        {
            // Is moving and attacking, set strafe
            moveController.SetBool(IsAttacking, true);
            moveController.SetBool(IsMoving, true);
        }
        else
        {
            moveController.SetBool(IsAttacking, false);
            moveController.SetBool(IsMoving, false);
        }

        //Set sprite direction
        if (input.x < -0.01f)
        {
            playerRenderer.flipX = true;
        } 
        else if (input.x > 0.01f)
        {
            playerRenderer.flipX = false;
        }
        
        // Play sound if there was movement input
        if (input != Vector3.zero)
        {
            _audioEmitter.PlaySound(footstepsHash);
        }
    }

    private void OnDestroy()
    {
        // Un-subscribe from events
        InputManager.OnAttack -= AttackAction;
        //InputManager.ChangeElement -= ChangeElementAction;

		//PlayerData.Instance.PlayerController = null;
        
        // Player HUD De-Binds
        InputManager.OnChangeElement -= SwitchAmmoSlotHUD;
	}

    /// <summary>
    /// Function that switches the HUD Display to the desired ammo slot
    /// </summary>
    /// <param name="isNext"></param>
    private void SwitchAmmoSlotHUD(bool isNext)
    {
        ammoManager.ChangeElement(isNext);
    }

    private void AttackAction()
	{
        // Check that there's a camera tagged with main
        if (Camera.main == null)
        {
            // There wasnt
            Debug.LogError("ERROR! MAIN CAMERA WAS NULL! CANT FIRE PROJECTILES FROM PLAYER");
            return;
        }
        
        // Every scene must have a ground object to raycast onto
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        bool didHit = Physics.Raycast(ray, out RaycastHit hitInfo, 100f, _groundLayers);

        if (!didHit) return; // No hit, return early
        
        // Compute Direction
        Vector3 direction = new Vector3(hitInfo.point.x - transform.position.x, 0, hitInfo.point.z - transform.position.z).normalized;
        
        // Call the fire function on the ammo manager
        ammoManager.FireCurrentElement(transform.position, direction);

        //Set sprite direction
        if (direction.x < -0.01f)
        {
            playerRenderer.flipX = true;
        }
        else if (direction.x > 0.01f)
        {
            playerRenderer.flipX = false;
        }
        moveController.SetBool(IsAttacking, true);
    }

    /// <summary>
    /// Routine that handles the orientation of the firing arrow placed at the player's feet
    /// </summary>
    private IEnumerator ProjectileDirRoutine()
    {
        // Variables Pre-Declared to optimize memory allocation
        RaycastHit hitInfo;
        Ray ray;
        bool didHit;
        Vector3 direction = new Vector3();
        Quaternion lookRotation;
        
        // Run while active
        while (true)
        {
            // Dont run this code if there isnt a main camera set up
            if (!Camera.main)
            {
                yield return null;
                continue;
            }
            
            // TODO: Add a bool gate in case we need to hide hud during an event or something
            
            // Get the location of the mouse
            ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            didHit = Physics.Raycast(ray, out hitInfo, 100f, _groundLayers);
            
            // No hit, restart loop
            if (!didHit)
            {
                yield return null;
                continue;
            }
        
            // Compute Direction and Set the firing arrow to the direction
            direction.x = hitInfo.point.x - transform.position.x;
            direction.z = hitInfo.point.z - transform.position.z;
            direction.y = 0;
            Vector3.Normalize(direction);
            if (direction != Vector3.zero)
            {
                lookRotation = Quaternion.LookRotation(direction, Vector3.up);
                projectileDirArrow.transform.rotation = Quaternion.Euler(90f, lookRotation.eulerAngles.y - 45f, 0f);
            }
            yield return null;
        }
    }

}
