// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci.Extensions.CSharp;

namespace Microsoft.Cci.Writers.CSharp
{
    public partial class CSDeclarationWriter
    {
        private void WriteFieldDefinition(IFieldDefinition field)
        {
            if (field.IsSpecialName)
                return;

            WriteAttributes(field.Attributes);
            if (!field.IsStatic && field.ContainingTypeDefinition.Layout == LayoutKind.Explicit)
            {
                WriteFakeAttribute("System.Runtime.InteropServices.FieldOffsetAttribute", field.Offset.ToString());
            }

            if (field.IsNotSerialized)
            {
                WriteFakeAttribute("System.NonSerializedAttribute");
            }

            if (!field.ContainingTypeDefinition.IsEnum)
            {
                WriteVisibility(field.Visibility);
                WriteCustomModifiers(field.CustomModifiers);

                if (field.Type.IsUnsafeType())
                    WriteKeyword("unsafe");

                if (field.IsCompileTimeConstant)
                {
                    if (field.GetHiddenBaseField(_filter) != Dummy.Field)
                        WriteKeyword("new");

                    WriteKeyword("const");
                }
                else
                {
                    if (field.IsStatic)
                        WriteKeyword("static");
                    if (field.IsReadOnly)
                        WriteKeyword("readonly");
                }

                if (!field.IsCompileTimeConstant && field.GetHiddenBaseField(_filter) != Dummy.Field)
                    WriteKeyword("new");

                WriteTypeName(field.Type);
                WriteIdentifier(field.Name);

                if (field.Constant != null && field.IsCompileTimeConstant)
                {
                    WriteSpace();
                    WriteSymbol("=", true);
                    if (field.Type.IsEnum) {
                        WriteFieldDefinitionValue(field);
                    } else {
                        WriteMetadataConstant(field.Constant);
                    }
                }

                WriteSymbol(";");
            }
            else
            {
                WriteIdentifier(field.Name);
                if (field.Constant != null && field.Constant.Value != null)
                {
                    WriteSpace();
                    WriteSymbol("=", true);
                    WriteMetadataConstant(field.Constant);
                }
                WriteSymbol(",");
            }
        }

        private void WriteFieldDefinitionValue(IFieldDefinition field) {
            var resolvedType = field.Type.ResolvedType;

            if (resolvedType != null) {
                foreach (var enumField in resolvedType.Fields) {
                    var enumFieldValue = enumField?.Constant?.Value;
                    if (enumFieldValue != null && enumFieldValue.Equals(field.Constant.Value))
                    {
                        WriteTypeName(field.Type, noSpace: true);
                        WriteSymbol(".");
                        WriteIdentifier(enumField.Name);
                        return;
                    }
                }
            }

            // couldn't find a symbol for enum, just cast it
            WriteSymbol("(");
            WriteTypeName(field.Type, noSpace: true);
            WriteSymbol(")");
            WriteMetadataConstant(field.Constant);
        }
    }
}
