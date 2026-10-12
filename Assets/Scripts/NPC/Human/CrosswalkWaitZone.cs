using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class CrosswalkWaitZone : MonoBehaviour
{
    private enum SignalState
    {
        CarGreen,
        PedestrianGreen,
    }

    [Header("신호 타이밍")]
    [Tooltip("차가 지나가는 시간 (보행자 빨간불 유지 시간)")]
    [SerializeField]
    private float _carGreenDuration = 8f;

    [Tooltip("보행자가 건너는 시간 (차 빨간불 유지 시간)")]
    [SerializeField]
    private float _pedestrianGreenDuration = 5f;

    [Tooltip("게임 시작 시 보행자 초록불로 먼저 시작할지 여부")]
    [SerializeField]
    private bool _startWithPedestrianGreen = false;

    [Header("연동 차량 정지 구역")]
    [Tooltip("이 신호와 동기화할 CarStopZone 목록")]
    [SerializeField]
    private CarStopZone[] _linkedCarStopZones;

    private SignalState _signal;
    private readonly List<NPCMovement> _waitingNPCs = new();
    private CancellationTokenSource _cycleCts;

    private void Start()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        CycleAsync(CtsUtility.Renew(ref _cycleCts)).Forget();
    }

    private void OnDisable()
    {
        CtsUtility.Cancel(ref _cycleCts);
    }

    private async UniTaskVoid CycleAsync(CancellationToken ct)
    {
        try
        {
            if (_startWithPedestrianGreen)
            {
                SetSignal(SignalState.PedestrianGreen);
                await WaitSeconds(_pedestrianGreenDuration, ct);
            }

            while (true)
            {
                SetSignal(SignalState.CarGreen);
                await WaitSeconds(_carGreenDuration, ct);

                SetSignal(SignalState.PedestrianGreen);
                ReleaseAllNPCs();
                await WaitSeconds(_pedestrianGreenDuration, ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    private static UniTask WaitSeconds(float sec, CancellationToken ct) =>
        UniTask.Delay(TimeSpan.FromSeconds(sec), DelayType.DeltaTime, cancellationToken: ct);

    private void SetSignal(SignalState next)
    {
        _signal = next;
        bool carGreen = next == SignalState.CarGreen;

        if (_linkedCarStopZones == null)
            return;

        foreach (var zone in _linkedCarStopZones)
        {
            if (zone == null)
                continue;
            if (carGreen)
                zone.OnSignalCarGreen();
            else
                zone.OnSignalPedestrianGreen();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_signal != SignalState.CarGreen)
            return;

        var npc = other.GetComponent<NPCMovement>();
        if (npc != null && !_waitingNPCs.Contains(npc))
        {
            npc.SetExternalPause(true);
            _waitingNPCs.Add(npc);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var npc = other.GetComponent<NPCMovement>();
        if (npc != null)
            _waitingNPCs.Remove(npc);
    }

    private void ReleaseAllNPCs()
    {
        foreach (var npc in _waitingNPCs)
            if (npc != null)
                npc.SetExternalPause(false);

        _waitingNPCs.Clear();
    }
}
