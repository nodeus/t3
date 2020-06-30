﻿using System;
using ImGuiNET;
using T3.Gui.UiHelpers;

namespace T3.Gui.InputUi
{
    public class EnumInputUi<T> : InputValueUi<T> where T : Enum
    {
        public override IInputUi Clone()
        {
            return new EnumInputUi<T>
                   {
                       InputDefinition = InputDefinition,
                       Parent = Parent,
                       PosOnCanvas = PosOnCanvas,
                       Relevancy = Relevancy
                   };
        }

        protected override InputEditStateFlags DrawEditControl(string name, ref T value)
        {
            var enumInfo = EnumCache.Instance.GetEnumEntry<T>();

            if (enumInfo.IsFlagEnum)
            {
                // show as checkboxes
                InputEditStateFlags editStateFlags = InputEditStateFlags.Nothing;
                if (ImGui.TreeNode("##enumParam124"))
                {
                    bool[] checks = enumInfo.SetFlags;
                    int intValue = (int)(object)value;
                    for (int i = 0; i < enumInfo.ValueNames.Length; i++)
                    {
                        int enumValueAsInt = enumInfo.ValuesAsInt[i];
                        checks[i] = (intValue & enumValueAsInt) > 0;
                        if (ImGui.Checkbox(enumInfo.ValueNames[i], ref checks[i]))
                        {
                            // value modified, store new flag
                            if (checks[i])
                            {
                                intValue |= enumValueAsInt;
                            }
                            else
                            {
                                intValue &= ~enumValueAsInt;
                            }

                            value = (T)(object)intValue;
                            editStateFlags |= InputEditStateFlags.Modified;
                        }

                        if (ImGui.IsItemClicked())
                        {
                            editStateFlags |= InputEditStateFlags.Started;
                        }

                        if (ImGui.IsItemDeactivatedAfterEdit())
                        {
                            editStateFlags |= InputEditStateFlags.Finished;
                        }
                    }

                    ImGui.TreePop();
                }

                return editStateFlags;
            }
            else
            {
                int index = Array.IndexOf(enumInfo.Values, value);
                InputEditStateFlags editStateFlags = InputEditStateFlags.Nothing;
                bool modified = ImGui.Combo("##dropDownParam", ref index, enumInfo.ValueNames, enumInfo.ValueNames.Length);
                if (modified)
                {
                    value = enumInfo[index];
                    editStateFlags |= InputEditStateFlags.ModifiedAndFinished;
                }

                if (ImGui.IsItemClicked())
                {
                    editStateFlags |= InputEditStateFlags.Started;
                }

                return editStateFlags;
            }
        }

        protected override void DrawReadOnlyControl(string name, ref T value)
        {
            ImGui.Text(value.ToString());
        }
    }
}