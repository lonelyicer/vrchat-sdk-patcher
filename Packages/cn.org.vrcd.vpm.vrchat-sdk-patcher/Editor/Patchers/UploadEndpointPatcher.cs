﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using VRC.SDKBase.Editor.Api;

namespace VRCD.VRChatPackages.VRChatSDKPatcher.Editor.Patchers
{
    internal class UploadEndpointPatcher : IPatcher
    {
        private MethodInfo _uploadSimpleMethodInfo = GetUploadSimpleMethod();

        public UploadEndpointPatcher()
        {
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            var isValueArgCodeFound = false;
            var insertIndex = -1;
            FieldInfo uploadUrlField = null;
            for (var index = 0; index < codes.Count; index++)
            {
                var code = codes[index];
                if (isValueArgCodeFound)
                {
                    if (code.opcode != OpCodes.Stfld)
                        continue;

                    if (code.operand is FieldInfo field && field.Name.Contains("<uploadUrl>"))
                    {
                        insertIndex = index;
                        uploadUrlField = field;
                        break;
                    }
                }

                if (code.opcode == OpCodes.Ldstr && code.operand is "url")
                {
                    isValueArgCodeFound = true;
                }
            }

            // uploadUrl = uploadUrl.Replace("//vrchat.com", "//api.vrchat.cloud");

            var settings = Settings.LoadSettings();

            var instructionsToInsert = new List<CodeInstruction>();
            instructionsToInsert.Add(new CodeInstruction(OpCodes.Ldarg_0));
            instructionsToInsert.Add(new CodeInstruction(OpCodes.Ldarg_0));
            instructionsToInsert.Add(new CodeInstruction(OpCodes.Ldfld, uploadUrlField));
            instructionsToInsert.Add(new CodeInstruction(OpCodes.Ldstr, "//vrchat.com"));
            instructionsToInsert.Add(new CodeInstruction(OpCodes.Ldstr, "//" + settings.ReplaceUploadUrlText));
            instructionsToInsert.Add(new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(String), nameof(string.Replace), new Type[] {typeof(string), typeof(string)})));
            instructionsToInsert.Add(new CodeInstruction(OpCodes.Stfld, uploadUrlField));

            codes.InsertRange(insertIndex + 1, instructionsToInsert);

            return codes;
        }

        private static MethodInfo GetUploadSimpleMethod()
        {
            var uploadSimpleMethod =
                typeof(VRCApi).GetMethod("UploadSimple", BindingFlags.NonPublic | BindingFlags.Static);

            var stateMachineAttr = uploadSimpleMethod.GetCustomAttribute<AsyncStateMachineAttribute>();
            var moveNextMethod =
                stateMachineAttr.StateMachineType.GetMethod("MoveNext", BindingFlags.NonPublic | BindingFlags.Instance);

            return moveNextMethod;
        }

        public void Patch(Harmony harmony)
        {
            var transpilerMethod =
                typeof(UploadEndpointPatcher).GetMethod(nameof(Transpiler),
                    BindingFlags.Static | BindingFlags.NonPublic);

            harmony.Patch(_uploadSimpleMethodInfo, transpiler: new HarmonyMethod(transpilerMethod));
        }
    }
}