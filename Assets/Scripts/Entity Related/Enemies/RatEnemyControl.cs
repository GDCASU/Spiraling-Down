using Pathfinding;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/* -----------------------------------------------------------
 * Author: William
 * 
 * 
 * Modified By:
 * 
 */// --------------------------------------------------------

/* -----------------------------------------------------------
 * Purpose: Control script for rat enemies
 * 
 */// --------------------------------------------------------


/// <summary>
/// 
/// </summary>
public class RatEnemyControl : MonoBehaviour
{
	[Header("Control")]
	[SerializeField] private float _minIdleTime = 2f;
	[SerializeField] private float _maxIdleTime = 5f;
	[SerializeField] private float _minRunTime = 1f;
	[SerializeField] private float _maxRunTime = 3f;
	[SerializeField] private float _attackTime = 0.5f;
	[SerializeField] private float _maxAttackHeight = 0.25f;
	[SerializeField] private float _runSpeed = 3f;
	// The rat will detect the player if the player is within x degrees of the direction it is looking
	[SerializeField] private float _detectionAngle = 30f;
    [SerializeField] private float _moveDistance = 5f;
    
    [Header("Data")]
    [SerializeField] private RatStats _stats;
// Bool that will make it so it uses values set in the inspector
    [SerializeField] private bool _dontLoadStats; 

	[Header("Objects")]
	[SerializeField] private GameObject _detectedUI;
	[SerializeField] private GameObject _spriteObj;

	// Use this bool to gate all your Debug.Log Statements please
	[Header("Debugging")]
	[SerializeField] private bool doDebugLog;

	private Animator _anim;
	private AIPath _aiPath;
	private Transform _target;
	private bool _attacking;
	private int _lookDir;
	private readonly Vector3[] _dirToVector = new Vector3[] { Vector3.back, Vector3.forward, Vector3.left, Vector3.right };

	void Start()
	{
		_anim = GetComponent<Animator>();
		_aiPath = GetComponent<AIPath>();
		_target = transform.Find("Target");
		_target.parent = null;
        
        // Load the data
        if (!_dontLoadStats)
        {
            _minIdleTime = _stats.minIdleTime;
            _maxIdleTime = _stats.maxIdleTime;
            _minRunTime = _stats.minRunTime;
            _maxRunTime = _stats.maxRunTime;
            _attackTime = _stats.attackTime;
            _maxAttackHeight = _stats.maxAttackHeight;
            _aiPath.maxSpeed = _stats.baseSpeed;
            _detectionAngle = _stats.detectionAngle;
        }
		
		_aiPath.maxAcceleration = 999999999f;
		_lookDir = 0;
		GetComponent<AIDestinationSetter>().target = _target;
		StartCoroutine(Idle());
	}

	private void OnDestroy()
	{
		if (_target != null) Destroy(_target.gameObject);
	}

	private void Update()
	{
		// If already attacking, return
		if(_attacking) return;

		// Check if the rat is looking at the player
		Vector3 ratToPlayer = new Vector3(
			PlayerObject.Instance.transform.position.x - transform.position.x,
			0f, 
			PlayerObject.Instance.transform.position.z - transform.position.z);
		float angleToPlayer = Vector3.Angle(ratToPlayer, _dirToVector[_lookDir]);

		// If the rat detects the player, stop current movement and attack
		if(angleToPlayer < _detectionAngle)
		{
			StopAllCoroutines();

			// Prevent AI from interrupting attack
			_aiPath.canMove = false;

			StartCoroutine(Attack());
		}
	}

	private IEnumerator Idle()
	{
		if(doDebugLog) Debug.Log(gameObject.name + " Idle");

		// Animation to idle state
		yield return _anim.PlayBlocking("SideToIdle");

		// Small chance to dance
		_anim.Play(Random.Range(0, 100) == 0 ? "Dance" : "Idle");
		yield return new WaitForSeconds(Random.Range(_minIdleTime, _maxIdleTime));

		StartCoroutine(Wander());
	}

	private IEnumerator Wander()
	{
		if(doDebugLog) Debug.Log(gameObject.name + " Wander");

		// Skitt in some cardinal direction
		_lookDir = Random.Range(0, 4);
		if(_lookDir == 0)
		{
			yield return _anim.PlayBlocking("IdleToSide");
			_anim.Play("RunForward");
		}
		else if(_lookDir == 1)
		{
			yield return _anim.PlayBlocking("IdleToSide");
			_anim.Play("RunBack");
		}
		else
		{
			_spriteObj.transform.localScale = new Vector3(_lookDir == 2 ? 2f : -2f, 2f, 2f);
			yield return _anim.PlayBlocking("IdleToSide");
			_anim.Play("RunSide");
		}
		_target.position = transform.position + _moveDistance * _dirToVector[_lookDir];

		// Move in the cardinal direction
		_aiPath.canMove = true;
		yield return new WaitForSeconds(Random.Range(_minRunTime, _maxRunTime));
		_aiPath.canMove = false;

		// Return to idle state
		StartCoroutine(Idle());
		yield break;
	}

	private IEnumerator Attack()
	{
		_attacking = true;

		// Pause and play antic animation
		_anim.Play("Idle");
		_detectedUI.SetActive(true);
		yield return new WaitForSeconds(1f);
		_detectedUI.SetActive(false);

		// Play attack animation
		if(_lookDir == 0)
		{
			_anim.Play("LungeForward");
		}
		else if(_lookDir == 1)
		{
			_anim.Play("LungeBack");
		}
		else
		{
			_spriteObj.transform.localScale = new Vector3(_lookDir == 2 ? 2f : -2f, 2f, 2f);
			_anim.Play("LungeSide");
		}

		// Attack in the direction the rat is looking
		float timer = 0f;
		while(timer < _attackTime)
		{
			float percent = timer / _attackTime;

			// Move sprite up and down to have a parabolic arc
			float yPos = 0.2f + 0.25f * (1f - Mathf.Pow(2f * percent - 1f, 2f));
			_spriteObj.transform.localPosition = new Vector3(0f, yPos);

			transform.position += _runSpeed * _dirToVector[_lookDir] * Time.deltaTime;

			timer += Time.deltaTime;
			yield return null;
		}
		_spriteObj.transform.localPosition = new Vector3(0f, 0.2f);

		// Return to wandering state
		StartCoroutine(Wander());
		_attacking = false;
		yield break;
	}
}
