using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Tilemaps;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class PurificationChecks
{
    const string Key="Rotline.PurificationChecks";
    static double next;
    static int phase;
    static int shots, stoppedShots;
    static WeaponAttackRuntime weapon;
    static Vector2 target;
    static float progress;
    static bool errors;
    static PurificationChecks()
    {
        if(!SessionState.GetBool(Key,false))return;
        BatchCheckGuard.RestoreProfile();
        next=EditorApplication.timeSinceStartup+3;
        Application.logMessageReceived += OnLog;
        EditorApplication.update+=Tick;
    }
    public static void RunBatch()
    {
        BatchCheckGuard.Run(Key, 45, () =>
        {
            BatchCheckGuard.Gameplay();
            EditorApplication.isPlaying = true;
        });
    }
    static void OnLog(string message, string trace, LogType type)
    {
        if (!trace.Contains("UnityEditor.Search.SearchDatabase") &&
            (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) errors = true;
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying || EditorApplication.timeSinceStartup<next)return;
        try
        {
            var field=Object.FindFirstObjectByType<CorruptionField>();var player=Object.FindFirstObjectByType<PlayerPurificationController>();var run=Object.FindFirstObjectByType<SurvivalRunController>();
            if(phase==0)
            {
                InputSystem.settings = Object.Instantiate(InputSystem.settings);
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                Object.FindFirstObjectByType<SpawnDirector>().SuspendSpawning=true;Object.FindFirstObjectByType<SpawnDirector>().ClearEnemies();
                run.Health.GrantInvulnerability(100);
                var settings=new SerializedObject(field);settings.FindProperty("seedRadius").intValue=0;settings.FindProperty("spreadInterval").floatValue=100;settings.ApplyModifiedPropertiesWithoutUndo();
                Require(Mathf.Approximately(settings.FindProperty("resistancePerCell").floatValue,.2f),"Gameplay purification must require 20% of the previous work.");
                var ground=(Tilemap)settings.FindProperty("groundTilemap").objectReferenceValue;
                bool found=false;
                foreach(var cell in ground.cellBounds.allPositionsWithin)
                {
                    var pos=(Vector2)ground.GetCellCenterWorld(cell);
                    if(Vector2.Distance(pos,player.transform.position)<1.5f || !player.CanPurifyAt(pos) || !Object.FindFirstObjectByType<NavigationService>().IsWalkable(pos))continue;
                    target=pos;found=true;break;
                }
                Require(found,"Need an accessible test tile within purification range.");
                Require(!player.CanPurifyAt((Vector2)player.transform.position+Vector2.right*100),"Out-of-range targeting must fail.");
                int before=field.CorruptedCellCount;
                Require(field.Seed(target)==1 && field.CorruptedCellCount==before+1,"Enemy seed must create an infected cell.");
                Require(field.Seed(target)==0,"Repeated seeds must not duplicate cells.");
                Require(field.ApplyPurification(target,.05f)==0 && Mathf.Abs(field.Progress(target)-.25f)<.001f,"Partial work must persist without clearing early.");
                Require(field.ApplyPurification(target,.15f)==1 && !field.IsCorrupted(target),"Full work must remove pollution.");
                Require(field.IsProtected(target) && field.Seed(target)==0,"Purified ground must resist reinfection.");
                Require(field.Purify(target)==0 && field.TotalPurifiedCells==1,"Empty cleansing must not grant duplicate credit.");
                // Use a longer test cell to inspect partial auto-work and pausing across frames.
                settings.FindProperty("resistancePerCell").floatValue=1f;settings.ApplyModifiedPropertiesWithoutUndo();
                weapon = Runtime(player); weapon.AttackActiveStarted += _ => shots++;
                MouseInput(false);
                phase++;next=EditorApplication.timeSinceStartup+field.ProtectionSeconds+.3;
            }
            else if(phase==1)
            {
                Require(shots >= 2, "Normal attacks must repeat without mouse input.");
                stoppedShots = shots;
                Require(!field.IsProtected(target) && field.Seed(target)==1,"Protection must expire and allow new infection.");
                player.GetComponent<PlayerModeController>().SetMode(PlayerMode.Purification);
                MouseInput(false);phase++;next=EditorApplication.timeSinceStartup+.15;
            }
            else if(phase==2)
            {
                MouseInput(false);phase++;next=EditorApplication.timeSinceStartup+.35;
            }
            else if(phase==3)
            {
                Require(shots == stoppedShots, "Switching to purification must stop automatic attacks.");
                progress=field.Progress(target);Require(progress>0 && field.IsCorrupted(target),"Purification must progress without holding any button.");
                Capture();run.TogglePause();phase++;next=EditorApplication.timeSinceStartup+.5;
            }
            else if(phase==4)
            {
                Require(Mathf.Abs(field.Progress(target)-progress)<.001f,"Pause must stop purification work.");
                MouseInput(false);run.TogglePause();phase++;next=EditorApplication.timeSinceStartup+.15;
            }
            else if(phase==5)
            {
                MouseInput(false);phase++;next=EditorApplication.timeSinceStartup+1.2;
            }
            else if(phase==6)
            {
                Require(!field.IsCorrupted(target) && field.TotalPurifiedCells==2,"Automatic purification must complete after resume.");
                player.GetComponent<PlayerCombatController>().EquipWeapon(AssetDatabase.LoadAssetAtPath<WeaponAttackDefinition>("Assets/Data/CombatExamples/Weapon_ChargedBeam.asset"));
                weapon=Runtime(player);shots=0;weapon.AttackActiveStarted+=_=>shots++;
                player.GetComponent<PlayerModeController>().SetMode(PlayerMode.Combat);
                MouseInput(false);phase++;next=EditorApplication.timeSinceStartup+1.5;
            }
            else if(phase==7)
            {
                Require(weapon.RequiresChargeInput && !weapon.IsCharging && shots==0,"Charge attacks must not start automatically.");
                MouseInput(true);phase++;next=EditorApplication.timeSinceStartup+1.4;
            }
            else if(phase==8)
            {
                Require(weapon.IsCharging && shots==0,"Holding must charge without firing.");
                MouseInput(false);phase++;next=EditorApplication.timeSinceStartup+.4;
            }
            else if(phase==9)
            {
                Require(!weapon.IsCharging && shots==1,"Releasing must fire exactly one charge attack.");
                phase++;next=EditorApplication.timeSinceStartup+1.5;
            }
            else if(phase==10)
            {
                Require(shots==1,"Charge attack must not repeat automatically after release.");
                MouseInput(true);phase++;next=EditorApplication.timeSinceStartup+.3;
            }
            else if(phase==11)
            {
                Require(weapon.IsCharging,"Second press must charge again.");run.TogglePause();
                MouseInput(false);phase++;next=EditorApplication.timeSinceStartup+.3;
            }
            else
            {
                Require(!weapon.IsCharging && shots==1,"Pause must cancel charging without firing.");
                Require(!errors,"Actions must not log game errors.");
                Debug.Log("[PurificationChecks] Automatic attacks and purification without clicking, mode isolation, pause/resume, charge-only press/hold/release and cancellation passed.");Exit(0);
            }
        }
        catch(Exception ex){Debug.LogException(ex);Exit(1);}
    }
    static WeaponAttackRuntime Runtime(PlayerPurificationController player) => (WeaponAttackRuntime)typeof(PlayerCombatController).GetField("weaponRuntime",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(player.GetComponent<PlayerCombatController>());
    static void MouseInput(bool held)
    {
        var mouse=Mouse.current??InputSystem.AddDevice<Mouse>();var state=new MouseState{position=Camera.main.WorldToScreenPoint(target)};
        if(held)state=state.WithButton(MouseButton.Left);InputSystem.QueueStateEvent(mouse,state);
    }
    static void Capture()
    {
        Canvas.ForceUpdateCanvases();var camera=Camera.main;var targetTexture=new RenderTexture(1280,720,24);var old=camera.targetTexture;var active=RenderTexture.active;var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try{camera.targetTexture=targetTexture;camera.Render();RenderTexture.active=targetTexture;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes("Logs/Purification.png",texture.EncodeToPNG());}
        finally{camera.targetTexture=old;RenderTexture.active=active;targetTexture.Release();Object.Destroy(targetTexture);Object.Destroy(texture);}
    }
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    static void Exit(int code)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        BatchCheckGuard.Finish(Key, code);
    }
}
