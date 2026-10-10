using MaxyGames.UNode;
using UnityEngine;

[NodeMenu("Samples/Flow", "Cooldown")]
public class Cooldown : IInstanceNode {
	[Input(typeof(float))]
	public ValuePortDefinition duration;
	[Output(type = typeof(Kind))]
	public FlowPortDefinition kind;

	public enum Kind {
		[PortDiscard]
		None,
		[PortDescription(description = "The flow to execute when cooldown is started")]
		Started,
		[PortDescription(description = "The flow to execute every cooldown is updated")]
		Updated,
		[PortDescription(description = "The flow to execute after completed")]
		Completed,
	}

	private float cooldownDuration;
	private float cooldownTimer;
	private bool isCoolingDown;

	[Output]
	public bool IsReady { get; private set; }
	[Output]
	public bool IsNotReady => !IsReady;
	[Output]
	public bool Tick { get; private set; }
	[Output]
	public bool Completed { get; private set; }
	[Output]
	public float Elapsed_Time => cooldownDuration - cooldownTimer;
	[Output]
	public float ElapsedPercent => Elapsed_Time / cooldownDuration;
	[Output]
	public float RemainingTime => cooldownTimer;
	[Output]
	public float RemainingPercent => RemainingTime / cooldownDuration;

	[Input("Start")]
	public void StartCooldown(float duration, out Kind kind) {
		if(!isCoolingDown) {
			isCoolingDown = true;
			IsReady = false;
			Tick = false;
			Completed = false;
			cooldownTimer = duration;
			kind = Kind.Started;
		}
		else {
			kind = Kind.None;
		}
	}

	[Input("Update")]
	public void UpdateCooldown(out Kind kind) {
		if(isCoolingDown) {
			cooldownTimer -= Time.deltaTime;

			if(cooldownTimer <= 0f) {
				isCoolingDown = false;
				cooldownTimer = 0f;
				IsReady = true;
				Completed = true;
				kind = Kind.Completed;
			}
			else {
				Tick = true;
				kind = Kind.Updated;
			}
		}
		else {
			kind = Kind.None;
		}
	}

	[Input("Reset")]
	public void ResetCooldown() {
		isCoolingDown = false;
		IsReady = true;
		Tick = false;
		Completed = false;
		cooldownTimer = 0f;
	}
}
