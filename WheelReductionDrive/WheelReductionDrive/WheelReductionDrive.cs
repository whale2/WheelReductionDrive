using System;
using ModuleWheels;
using UnityEngine;

namespace WheelReductionDrive
{
    public class WheelReductionDrive : PartModule, IPartMassModifier
    {
        /* Adjusting torque curve with simple reduction ratio 
         * Adjusting wheel stress tolerance in exchange of weight and EC consuming
         * Adding action group for switching steering reverse
         * 
         */

        [KSPField(isPersistant = false, guiActiveEditor = true, guiName = "Reductor ratio", guiFormat = "0.0")]
        [UI_FloatRange(affectSymCounterparts = UI_Scene.All, 
            controlEnabled = true, maxValue = 3f, minValue = 0.3f, scene = UI_Scene.Editor)]
        public float guiReductorRatio = -1.0f;

        [KSPField(isPersistant = true)]
        // reductor ratio is torque mutiplier (and speed de-multiplier)
        public float reductorRatio = 1.0f;
        
        public float tweakScaleRatio = 1.0f;

        [KSPField(isPersistant = false, guiActiveEditor = true, guiName = "Endurance/Mass", guiFormat = "0.0")]
        [UI_FloatRange(affectSymCounterparts = UI_Scene.All, 
            controlEnabled = true, maxValue = 3f, minValue = 0.5f, scene = UI_Scene.Editor)]
        public float guiEnduranceMassModifier = 1.0f;

        [KSPField(isPersistant = true)]
        public float enduranceMassModifier = 1.0f;
        
        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            guiReductorRatio = reductorRatio;
            guiEnduranceMassModifier = enduranceMassModifier;
            SetupReductor();
            SetupEndurance();
            GameEvents.onEditorShipModified.Add(OnEditorShipModified);
        }

        private void OnDestroy()
        {
            GameEvents.onEditorShipModified.Remove(OnEditorShipModified);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (HighLogic.LoadedSceneIsEditor) 
            {
                SetupReductor();
                SetupEndurance();
            }
        }

        public void OnEditorShipModified(ShipConstruct sc)
        {
            enduranceMassModifier = guiEnduranceMassModifier;
            if (reductorRatio != guiReductorRatio)
            {
                reductorRatio = guiReductorRatio;
                SetupReductor();
            }

            if (enduranceMassModifier != guiEnduranceMassModifier)
            {
                reductorRatio = guiReductorRatio;
                SetupEndurance();
            }
        }

        public void checkTweakScale()
        {
            // Check if TweakScale is present
            if (part.Modules.Contains("TweakScale"))
            {
                PartModule tweakScale = part.Modules["TweakScale"];
                var scale = tweakScale.Fields.GetValue("currentScale");
                var defaultScale = tweakScale.Fields.GetValue("defaultScale");
                if (scale != null && defaultScale != null)
                {
                    tweakScaleRatio = (float)Math.Pow((float)scale / (float)defaultScale, 3);
                    // TweakScale uses cubed scale to calculate torque
                    Debug.Log(
                        $"Reductor: found TweakScale; current={scale}, default={defaultScale}, " +
                        $"scaled reductor={tweakScaleRatio}, reductor={reductorRatio}");
                }
            }
        }
        
        public void SetupReductor()
        {
            Debug.Log("Reductor: Setup called");
                     
            Part prefab = part.partInfo.partPrefab;
            ModuleWheelMotor prefabMotor = prefab.Modules.GetModule<ModuleWheelMotor>();
            if (prefabMotor == null)
            {
                return;
            }
            guiReductorRatio = reductorRatio;
            checkTweakScale();
            
            float maxTorque = 0;
            // Get curve from prefab
            FloatCurve newCurve = new FloatCurve();
            
            ModuleWheelMotor motor = part.Modules.GetModule<ModuleWheelMotor>();
            foreach (Keyframe kf in prefabMotor.torqueCurve.Curve.keys)
            {
                Debug.Log($"Reductor: Prefab Curve: {kf.time} -> {kf.value}");
                newCurve.Add(
                    kf.time / reductorRatio, kf.value * tweakScaleRatio * reductorRatio);
                maxTorque = kf.value > maxTorque ? kf.value : maxTorque;
            }
            foreach (Keyframe kf in newCurve.Curve.keys)
            {
                Debug.Log(String.Format("Reductor: New Curve: {0} -> {1}", kf.time, kf.value));
//                newCurve.Add(kf.time * effectiveReductorRatio, kf.value / effectiveReductorRatio);
//                maxTorque = kf.value > maxTorque ? kf.value : maxTorque;
            }
            
            motor.torqueCurve = newCurve;
            motor.wheelSpeedMax = prefabMotor.wheelSpeedMax * tweakScaleRatio / reductorRatio ;
            motor.maxTorque = maxTorque * tweakScaleRatio * reductorRatio ;
            Debug.Log($"MaxTorque={motor.maxTorque}, MaxSpeed={motor.wheelSpeedMax}");
        }

        public void SetupEndurance()
        {
            Debug.Log("Reductor: Enduracne Setup called");
            Part prefab = part.partInfo.partPrefab;
            ModuleWheelDamage prefabDamage = prefab.Modules.GetModule<ModuleWheelDamage>();
            if (prefabDamage == null)
            {
                return;
            }

            guiEnduranceMassModifier = enduranceMassModifier;
            checkTweakScale();
            
            ModuleWheelDamage wheelDamage = part.Modules.GetModule<ModuleWheelDamage>();
            wheelDamage.impactTolerance = prefabDamage.impactTolerance * tweakScaleRatio * enduranceMassModifier;
            wheelDamage.stressTolerance = prefabDamage.stressTolerance * tweakScaleRatio * enduranceMassModifier;
            float massModifier = part.mass * (enduranceMassModifier - 1);
            Debug.Log($"Reductor Endurance: impact = {wheelDamage.impactTolerance}, stress={wheelDamage.stressTolerance}" +
                      $" mass modifier={massModifier}");
        }

        public void FixedUpdate()
        {
            if (Input.GetKeyDown(KeyCode.Keypad5))
            {
                ModuleWheelMotor motorModule = part.Modules.GetModule<ModuleWheelMotor>();
                foreach (Keyframe kf in motorModule.torqueCurve.Curve.keys)
                {
                    Debug.Log($"Debug Reductor: {kf.time} -> {kf.value}");
                }

                Debug.Log(
                    $"Debug Max Speed: {motorModule.wheelSpeedMax}, Reductor Ratio: {reductorRatio}, " +
                    $"Max Torque: {motorModule.maxTorque}, Drive Output: {motorModule.driveOutput}");
                Debug.Log($"Reductor: gui={guiReductorRatio}, reductor={reductorRatio}");
            }
        }

        public override void OnActive()
        {
            base.OnActive();
            SetupReductor();
            SetupEndurance();
        }

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            return defaultMass * (enduranceMassModifier - 1);
        }

        public ModifierChangeWhen GetModuleMassChangeWhen()
        {
            return ModifierChangeWhen.FIXED;
        }
    }
}