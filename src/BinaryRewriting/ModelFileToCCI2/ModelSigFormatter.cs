// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci;
using Microsoft.Tools.Transformer.CodeModel;
using System;
using System.Text;

namespace ModelFileToCCI2
{
    // This class makes CCI2 entities to model.xml style signatures
    public class ModelSigFormatter : SignatureFormatter
    {
        private const NameFormattingOptions FieldFormatOpts =
            NameFormattingOptions.OmitContainingType |
            NameFormattingOptions.PreserveSpecialNames |
            NameFormattingOptions.OmitWhiteSpaceAfterListDelimiter |
            NameFormattingOptions.TypeParameters |
            NameFormattingOptions.UseReflectionStyleForNestedTypeNames;


        private const NameFormattingOptions FormatOpts =
            NameFormattingOptions.Signature |
            FieldFormatOpts;

        private TypeNameFormatter _formatter;
        public ModelSigFormatter() : this(new TypeNameFormatter()) { }
        private ModelSigFormatter(TypeNameFormatter formatter) : base(formatter) { _formatter = formatter; }

        public string GetMemberSignature(ITypeMemberReference member, bool TypeMemberPrefix)
        {
            string sig;
            IFieldReference typeRef = member as IFieldReference;
            if (typeRef != null) // CCI change where now fields Signature contains return type.
                sig = GetMemberSignature(member, FieldFormatOpts);
            else
                sig = GetMemberSignature(member, FormatOpts);

            IMethodReference method = member as IMethodReference;
            if (method != null && sig.Contains("()")) // CCI change where parameterless methods now return empty parenthesis.
            {
                sig = sig.Replace("()", "");
            }

            IPropertyDefinition property = member as IPropertyDefinition;
            if (property != null)
            {
                sig = sig.Replace("[", "(").Replace("]", ")");
            }

            if (TypeMemberPrefix)
            {
                string prefix = Enum.GetName(typeof(MemberTypes), TypeMemberType(member));
                return prefix + " : " + sig;
            }
            else
            {
                return sig;
            }
        }

        private static MemberTypes TypeMemberType(ITypeMemberReference member)
        {
            if (member is IMethodReference)
                return MemberTypes.Method;
            else if (member is IFieldReference)
                return MemberTypes.Field;
            else if (member is IPropertyDefinition)
                return MemberTypes.Property;
            else if (member is IEventDefinition)
                return MemberTypes.Event;
            else
                return MemberTypes.Unknown;
        }

        public string GetTypeSignature(ITypeReference type)
        {
            return _formatter.GetTypeName(type, (FormatOpts & ~NameFormattingOptions.OmitContainingType));
        }

        // Put return type after the signature
        public override string GetMethodSignature(IMethodReference method, NameFormattingOptions formattingOptions)
        {
            string baseSig = base.GetMethodSignature(method, (formattingOptions & ~NameFormattingOptions.ReturnType));
            StringBuilder sb = new StringBuilder(baseSig);
            AppendReturnTypeSignature(method, (formattingOptions | NameFormattingOptions.ReturnType), sb);
            return sb.ToString();
        }

        // Append "@" to a byref param's type
        protected override void AppendParameter(IParameterTypeInformation param, NameFormattingOptions formattingOptions, StringBuilder sb)
        {
            // HACK: This is to support CCI1 syntax where generic type arguments are not shown for nested types.
            if (param.Type is INestedTypeReference) formattingOptions = formattingOptions | NameFormattingOptions.OmitTypeArguments;

            base.AppendParameter(param, (formattingOptions & ~NameFormattingOptions.OmitContainingType), sb);

            if (param.IsByReference) sb.Append('@');

            if (param.IsModified)
            {
                int count = 0;
                foreach (ICustomModifier modifier in param.CustomModifiers)
                {
                    count++;
                    //if (count>1)
                    //{ throw new NotImplementedException("Can't create a signature with more than one custom modifier"); }
                    AppendCustomModifier(formattingOptions, sb, modifier);
                }
            }
        }

        private void AppendCustomModifier(NameFormattingOptions formattingOptions, StringBuilder sb, ICustomModifier modifier)
        {
            sb.Append(' ');
            sb.Append(modifier.IsOptional ? "modopt" : "modreq");
            sb.Append('(');
            sb.Append(_formatter.GetTypeName(modifier.Modifier, formattingOptions & ~NameFormattingOptions.OmitContainingType));
            sb.Append(')');
        }

        // ".ctor" -> "#ctor". NOTE: ".cctor" is unchanged
        protected override void AppendMethodName(IMethodReference method, NameFormattingOptions formattingOptions, StringBuilder sb)
        {
            base.AppendMethodName(method, formattingOptions, sb);
            sb.Replace(".ctor", "#ctor");
        }

        // Append return type only on op_Explicit methods
        protected override void AppendReturnTypeSignature(ISignature sig, NameFormattingOptions formattingOptions, StringBuilder sb)
        {
            if ((formattingOptions & NameFormattingOptions.ReturnType) == 0) return;
            ITypeMemberReference member = sig as ITypeMemberReference;
            // TODO: Review
            //if (sig.CallingConvention != CallingConvention.Default)
            //{
            //    sb.Append(' ');
            //    sb.Append(sig.CallingConvention);
            //}
            if (sig.ReturnValueIsModified)
            {
                foreach (ICustomModifier modifier in sig.ReturnValueCustomModifiers)
                {
                    AppendCustomModifier(formattingOptions, sb, modifier);
                }
            }
            //===========

            if (member == null || (member.Name.Value != "op_Explicit" && member.Name.Value != "op_Implicit")) return;

            sb.Append(" : ");
            sb.Append(_formatter.GetTypeName(sig.Type, formattingOptions & ~NameFormattingOptions.OmitContainingType));
        }

        protected virtual void AppendMethodModifiers(IMethodReference method, NameFormattingOptions formattingOptions, StringBuilder sb)
        {
        }
    }
}
