// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Cci.Extensions;
using Microsoft.Cci.Extensions.CSharp;
using Microsoft.Cci.Writers.Syntax;

namespace Microsoft.Cci.Writers.CSharp
{
    public partial class CSDeclarationWriter
    {
        private void WriteMethodDefinition(IMethodDefinition method)
        {
            if (method.IsPropertyOrEventAccessor())
                return;

            if (method.IsDestructor())
            {
                WriteDestructor(method);
                return;
            }

            string name = method.GetMethodName();

            WriteMethodPseudoCustomAttributes(method);

            WriteAttributes(method.Attributes);
            WriteAttributes(method.SecurityAttributes);

            if (!method.ContainingTypeDefinition.IsInterface)
            {
                if (!method.IsExplicitInterfaceMethod()) WriteVisibility(method.Visibility);
                WriteMethodModifiers(method);
            }
            WriteInterfaceMethodModifiers(method);
            WriteMethodDefinitionSignature(method, name);
            WriteMethodBody(method);
        }

        private void WriteDestructor(IMethodDefinition method)
        {
            WriteSymbol("~");
            WriteIdentifier(((INamedEntity)method.ContainingTypeDefinition).Name);
            WriteSymbol("(");
            WriteSymbol(")", false);
            WriteEmptyBody();
        }

        private void WriteMethodDefinitionSignature(IMethodDefinition method, string name)
        {
            bool isOperator = method.IsConversionOperator();

            if (!isOperator && !method.IsConstructor)
            {
                WriteAttributes(method.ReturnValueAttributes, true);
                // We are ignoring custom modifiers right now, we might need to add them later.
                WriteTypeName(method.Type, isDynamic: IsDynamic(method.ReturnValueAttributes));
            }

            WriteIdentifier(name);

            if (isOperator)
            {
                WriteSpace();
                WriteTypeName(method.Type);
            }

            Contract.Assert(!(method is IGenericMethodInstance), "Currently don't support generic method instances");
            if (method.IsGeneric)
                WriteGenericParameters(method.GenericParameters);

            WriteParameters(method.Parameters, extensionMethod: method.IsExtensionMethod(), acceptsExtraArguments: method.AcceptsExtraArguments);
            if (method.IsGeneric && !method.IsOverride() && !method.IsExplicitInterfaceMethod())
                WriteGenericContraints(method.GenericParameters);
        }

        private void WriteParameters(IEnumerable<IParameterDefinition> parameters, bool property = false, bool extensionMethod = false, bool acceptsExtraArguments = false)
        {
            string start = property ? "[" : "(";
            string end = property ? "]" : ")";

            WriteSymbol(start);
            _writer.WriteList(parameters, p =>
            {
                WriteParameter(p, extensionMethod);
                extensionMethod = false;
            });

            if (acceptsExtraArguments)
            {
                if (parameters.Any())
                    _writer.WriteSymbol(",");
                _writer.WriteSpace();
                _writer.Write("__arglist");
            }

            WriteSymbol(end);
        }

        private void WriteParameter(IParameterDefinition parameter, bool extensionMethod)
        {
            WriteAttributes(parameter.Attributes, true);

            if (extensionMethod)
                WriteKeyword("this");

            if (parameter.IsParameterArray)
                WriteKeyword("params");

            if (parameter.IsOut && !parameter.IsIn && parameter.IsByReference)
            {
                WriteKeyword("out");
            }
            else
            {
                // For In/Out we should not emit them until we find a scenario that is needs thems.
                //if (parameter.IsIn)
                //   WriteFakeAttribute("System.Runtime.InteropServices.In", writeInline: true);
                //if (parameter.IsOut)
                //    WriteFakeAttribute("System.Runtime.InteropServices.Out", writeInline: true);
                if (parameter.IsByReference)
                    WriteKeyword("ref");
            }

            WriteTypeName(parameter.Type, isDynamic: IsDynamic(parameter.Attributes));
            WriteIdentifier(parameter.Name);
            if (parameter.IsOptional && parameter.HasDefaultValue)
            {
                WriteSymbol("=");
                WriteMetadataConstant(parameter.DefaultValue, parameter.Type);
            }
        }

        private void WriteInterfaceMethodModifiers(IMethodDefinition method)
        {
            if (method.GetHiddenBaseMethod(_filter) != Dummy.Method)
                WriteKeyword("new");
        }

        private void WriteMethodModifiers(IMethodDefinition method)
        {
            if (method.IsMethodUnsafe())
                WriteKeyword("unsafe");

            if (method.IsStatic)
                WriteKeyword("static");

            if (method.IsVirtual)
            {
                if (method.IsNewSlot)
                {
                    if (method.IsAbstract)
                        WriteKeyword("abstract");
                    else if (!method.IsSealed) // non-virtual interfaces implementations are sealed virtual newslots
                        WriteKeyword("virtual");
                }
                else
                {
                    if (method.IsAbstract)
                        WriteKeyword("abstract");
                    else if (method.IsSealed)
                        WriteKeyword("sealed");
                    WriteKeyword("override");
                }
            }
        }

        private void WriteMethodBody(IMethodDefinition method)
        {
            if (method.IsAbstract || !_forCompilation)
            {
                WriteSymbol(";");
                return;
            }

            if (method.IsConstructor)
                WriteBaseConstructorCall(method.ContainingTypeDefinition);

            // Write Dummy Body
            WriteSpace();
            WriteSymbol("{", true);

            WriteOutParameterInitializations(method);

            if (_forCompilationThrowPlatformNotSupported)
            {
                Write("throw new ");
                if (_forCompilationIncludeGlobalprefix)
                    Write("global::");
                Write("System.PlatformNotSupportedException(); ");
            }
            else if (method.ContainingTypeDefinition.IsValueType && method.IsConstructor)
            {
                // Structs cannot have empty constructors so we need to output this dummy body
                Write("throw new ");
                if (_forCompilationIncludeGlobalprefix)
                    Write("global::");
                Write("System.NotImplementedException(); ");
            }
            else if (!TypeHelper.TypesAreEquivalent(method.Type, method.ContainingTypeDefinition.PlatformType.SystemVoid))
            {
                WriteKeyword("return");
                WriteDefaultOf(method.Type);
                WriteSymbol(";", true);
            }

            WriteSymbol("}");
        }

        private void WritePrivateConstructor(ITypeDefinition type)
        {
            if (!_forCompilation ||
                type.IsInterface ||
                type.IsEnum ||
                type.IsDelegate ||
                type.IsValueType ||
                type.IsStatic)
                return;

            WriteVisibility(TypeMemberVisibility.Assembly);
            WriteIdentifier(((INamedEntity)type).Name);
            WriteSymbol("(");
            WriteSymbol(")");
            WriteBaseConstructorCall(type);
            WriteEmptyBody();
        }

        private void WriteOutParameterInitializations(IMethodDefinition method)
        {
            if (!_forCompilation)
                return;

            var outParams = method.Parameters.Where(p => p.IsOut);

            foreach (var param in outParams)
            {
                WriteIdentifier(param.Name);
                WriteSpace();
                WriteSymbol("=", true);
                WriteDefaultOf(param.Type);
                WriteSymbol(";", true);
            }
        }

        private void WriteBaseConstructorCall(ITypeDefinition type)
        {
            if (!_forCompilation)
                return;

            ITypeDefinition baseType = type.BaseClasses.FirstOrDefault().GetDefinitionOrNull();

            if (baseType == null)
                return;

            var ctors = baseType.Methods.Where(m => m.IsConstructor && _filter.Include(m));

            var defaultCtor = ctors.Where(c => c.ParameterCount == 0);

            // Don't need a base call if we have a default constructor
            if (defaultCtor.Any())
                return;

            var ctor = ctors.FirstOrDefault();

            if (ctor == null)
                return;

            WriteSpace();
            WriteSymbol(":", true);
            WriteKeyword("base");
            WriteSymbol("(");
            _writer.WriteList(ctor.Parameters, p => WriteDefaultOf(p.Type));
            WriteSymbol(")");
        }

        private void WriteEmptyBody()
        {
            if (!_forCompilation)
            {
                WriteSymbol(";");
            }
            else
            {
                WriteSpace();
                WriteSymbol("{", true);
                WriteSymbol("}");
            }
        }

        private void WriteDefaultOf(ITypeReference type)
        {
            WriteKeyword("default", true);
            WriteSymbol("(");
            WriteTypeName(type, true);
            WriteSymbol(")");
        }

        public static IDefinition GetDummyConstructor(ITypeDefinition type)
        {
            return new DummyInternalConstructor() { ContainingType = type };
        }

        private class DummyInternalConstructor : IDefinition
        {
            public ITypeDefinition ContainingType { get; set; }

            public IEnumerable<ICustomAttribute> Attributes
            {
                get { throw new System.NotImplementedException(); }
            }

            public void Dispatch(IMetadataVisitor visitor)
            {
                throw new System.NotImplementedException();
            }

            public IEnumerable<ILocation> Locations
            {
                get { throw new System.NotImplementedException(); }
            }

            public void DispatchAsReference(IMetadataVisitor visitor)
            {
                throw new System.NotImplementedException();
            }
        }
    }
}
