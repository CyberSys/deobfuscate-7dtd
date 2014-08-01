using System;
using DeobfuscateMain;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace NetworkPatcher
{
	public class NetworkPatcher
	{
		public static string getName()
		{
			return "PacketClassesPatcher";
		}
		public static string[] getAuthors()
		{
			return new string[]{ "DerPopo" };
		}

		static MethodDefinition cctorMDef = null;
		static TypeDefinition packageTypeEnumDef = null;

		public static void Patch(Logger logger, AssemblyDefinition asmCSharp, AssemblyDefinition __reserved)
		{
			TypeDefinition packageClass = null;
			foreach (ModuleDefinition mdef in asmCSharp.Modules)
			{
				foreach (TypeDefinition tdef in mdef.Types)
				{
					foreach (FieldDefinition fdef in tdef.Fields)
					{
						if (fdef.Name.Equals ("m_PackageTypeToClass")) {
							packageClass = tdef;
							break;
						}
					}
					if (packageClass != null)
						break;
				}
				if (packageClass != null)
					break;
			}
			if (packageClass == null) {
				logger.Log("ERROR : Cannot find m_PackageTypeToClass!");
				return;
			}
			logger.Log("LOG : Found m_PackageTypeToClass!");
			packageClass.Name = "Package";
			foreach (MethodDefinition mdef in packageClass.Methods) {
				if (mdef.Name.Equals (".cctor")) {
					cctorMDef = mdef;
					continue;
				}
				if (mdef.IsStatic && mdef.ReturnType.Resolve ().Equals (packageClass) && mdef.Parameters.Count == 1) {
					(packageTypeEnumDef = mdef.Parameters[0].ParameterType.Resolve()).Name = "PackageType";
					mdef.Name = "CreatePackage";
					continue;
				}
			}
			if (cctorMDef == null) {
				logger.Log("ERROR : Cannot find Package.cctor()!");
				return;
			}
			logger.Log("LOG : Found Package.cctor()!");
			if (packageTypeEnumDef == null) {
				logger.Log("ERROR : Cannot find CreatePackage!");
				return;
			}
			logger.Log("LOG : Found CreatePackage!");
			bool curFound = false;
			foreach (MethodDefinition mdef in packageClass.Methods) {
				if (mdef.IsStatic && mdef.Parameters.Count == 1 && mdef.ReturnType.Resolve().Equals(packageTypeEnumDef) && mdef.Parameters[0].ParameterType.Resolve().FullName.Equals("System.IO.BinaryReader"))
				{
					mdef.Name = "ReadPackageType";
					curFound = true;
					break;
				}
			}
			if (!curFound) {
				logger.Log("WARNING : Cannot find ReadPackageType!");
			}
			logger.Log("LOG : Found ReadPackageType!");
			PatchVirtualPackageMethods(packageClass, logger);
			MethodBody cctorBody = cctorMDef.Body;
			cctorBody.SimplifyMacros();
			for (int i = 1; i < cctorBody.Instructions.Count; i++)
			{
				Instruction curInstr = cctorBody.Instructions[i];
				if (curInstr.OpCode == OpCodes.Ldtoken)
				{
					if (typeof(TypeReference).IsAssignableFrom(curInstr.Operand.GetType()))
					{
						TypeReference curPackageClass = (TypeReference)curInstr.Operand;
						Instruction lastInstr = cctorBody.Instructions[i - 1];
						if (lastInstr.OpCode == OpCodes.Ldc_I4)
						{
							FieldDefinition enumField = null;
							int enumFieldId = (Int32)lastInstr.Operand;
							logger.Log("LOG : Analyzing packet class (" + curPackageClass.FullName + "; " + enumFieldId + ")...");
							foreach (FieldDefinition curEnumField in packageTypeEnumDef.Fields)
							{
								if (curEnumField.HasConstant && curEnumField.Constant != null)
								{
									//logger.Log (curEnumField.Constant.GetType ().FullName);
									int curConst = -1;
									if (curEnumField.Constant.GetType () == typeof(Byte))
										curConst = (int)(((Byte)curEnumField.Constant));
									else if (curEnumField.Constant.GetType () == typeof(Int16))
										curConst = (int)(((Int16)curEnumField.Constant));
									else if (curEnumField.Constant.GetType () == typeof(Int32))
										curConst = (int)(((Int32)curEnumField.Constant));
									if (curConst == -1)
										logger.Log ("WARNING : Unknown const in packageTypeEnumDef.Fields!");
									else if (curConst == enumFieldId)
									{
										enumField = curEnumField;
										break;
									}
								}
							}
							if (enumField == null) {
								logger.Log ("WARNING : The package class uses an unknown PackageType!");
								curPackageClass.Name = "NetPackage_" + enumFieldId;
							} else if (containsAbnormalUnicode (enumField.Name)) {
								logger.Log ("INFO : The package class uses an obfuscated PackageType!");
								curPackageClass.Name = "NetPackage_" + enumFieldId;
							}
							else
								curPackageClass.Name = "NetPackage_" + enumField.Name;
							PatchVirtualPackageMethods(curPackageClass.Resolve(), logger);
							logger.Log("LOG : Renamed packet class (" + curPackageClass.FullName + ")!");
						}
						else
							logger.Log("WARNING : There is no ldc.i4 before the current ldtoken !");
					}
					else
						logger.Log("WARNING : A Ldtoken instruction has no TypeReference operand!");
				}
			}
			cctorBody.OptimizeMacros();
		}

		private static void PatchVirtualPackageMethods(TypeDefinition tdef, Logger logger)
		{
			foreach (MethodDefinition mdef in tdef.Methods) {
				if (mdef.IsVirtual && mdef.Parameters.Count == 0 && mdef.ReturnType.Resolve().Equals(packageTypeEnumDef))
				{
					mdef.Name = "GetPackageType";
					logger.Log("LOG : Found " + mdef.FullName + "!");
					continue;
				}
				if (mdef.IsVirtual && mdef.Parameters.Count == 1 && mdef.ReturnType.Resolve().FullName.Equals("System.Void") &&
					mdef.Parameters[0].ParameterType.Resolve().FullName.Equals("System.Int32"))
				{
					mdef.Name = "SetChannel";
					logger.Log("LOG : Found " + mdef.FullName + "!");
					continue;
				}
				if (mdef.IsVirtual && mdef.Parameters.Count == 1 && mdef.ReturnType.Resolve().FullName.Equals("System.Void") &&
					mdef.Parameters[0].ParameterType.Resolve().FullName.Equals("System.IO.BinaryReader"))
				{
					mdef.Name = "Read";
					logger.Log("LOG : Found " + mdef.FullName + "!");
					continue;
				}
				if (mdef.IsVirtual && mdef.Parameters.Count == 1 && mdef.ReturnType.Resolve().FullName.Equals("System.Void") &&
					mdef.Parameters[0].ParameterType.Resolve().FullName.Equals("System.IO.BinaryWriter"))
				{
					mdef.Name = "Write";
					logger.Log("LOG : Found " + mdef.FullName + "!");
					continue;
				}
				if (mdef.IsVirtual && mdef.Parameters.Count == 2 && mdef.ReturnType.Resolve().FullName.Equals("System.Void") &&
					mdef.Parameters[0].ParameterType.Resolve().FullName.Equals("World") &&
					mdef.Parameters[1].ParameterType.Resolve().FullName.Equals("INetConnectionCallbacks"))
				{
					mdef.Name = "Process";
					logger.Log("LOG : Found " + mdef.FullName + "!");
					continue;
				}
				if (mdef.IsVirtual && mdef.Parameters.Count == 0 && mdef.ReturnType.Resolve().FullName.Equals("System.Int32"))
				{
					mdef.Name = "GetEstimatedPackageSize";
					logger.Log("LOG : Found " + mdef.FullName + "!");
					continue;
				}
				if (mdef.IsVirtual && mdef.Parameters.Count == 0 && mdef.ReturnType.Resolve().FullName.Equals("System.String"))
				{
					mdef.Name = "GetFriendlyName";
					logger.Log("LOG : Found " + mdef.FullName + "!");
					continue;
				}
			}
		}


		private static bool containsAbnormalUnicode(String origName)
		{
			if (origName == null)
				return true;
			foreach (char ch in origName)
			{
				if (
					(
						((ch & 0x00FF) > 0x7F) || (((ch & 0xFF00) >> 8) > 0x7F)
					) ||
					(("" + ch).Normalize().ToCharArray()[0] > 0x00FF) ||
					(((("" + ch).Normalize().ToCharArray()[0] & 0x00FF)) <= 0x20)
				)
				{
					return true;
				}
			}
			return false;
		}

	}
}
