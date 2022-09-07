using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;

namespace rmk_checkerHNY
{
	class Program
	{
		/*				COLORS
		 *				
		 *				1 - White
		 *				2 - Blue
		 *				3 - Red
		 *				4 - Green
		 *				5 - Yellow
		 *				6 - DarkYellow
		 *				
		 *				
		 *				Check for fee if there is "if owner ==" or smt
		 * */

		class Contract {

			public string compilerVersion = string.Empty;
			public string tokenName = string.Empty;
			public ushort optimizationsUsed = 0;
			public string licenseType = string.Empty;

			public class Function {
				public string functionName = string.Empty;
				public string[] lines = new string[5000];
				public ushort at_line = 0;
			}
			public Function[] functions = new Function[5000];
			public ushort at_func = 0;

		}

		static string _getFunctionName(string line)
		{
			line = line.Replace("function ", string.Empty);
			int index = line.IndexOf("(");
			if (index > 0)
			{
				line = line.Substring(0, index);
				line = line.Replace(" ", string.Empty);
				return line;
			}
			return string.Empty;
		}

		static bool _onlyValidCharacters(string line)
		{
			for(int i = 0; i < line.Length; i++)
			{
				if (!((line[i] >= 97 && line[i] <= 122) || (line[i] >= 65 && line[i] <= 90) || (line[i] >= 48 && line[i] <= 57))) return false;
			}
			return true;
		}

		static bool _isValidContract(string line)
		{
			return ((line.Length == 42 && _onlyValidCharacters(line) && line.StartsWith("0x")) ? true : false);
		}

		static bool _stringContainsAddress(string line)
		{
			if (line.Contains("0x") && (line.IndexOf("0x") + 1) + 10 < line.Length && !line.Contains("0x0")) return true;
			return false;
		}

		static bool _isHoneypotFunction(string line)
		{
			if ((line.Contains("transferfrom") || line.Contains("TransferFrom") || line.Contains("transferFrom") || 
				line.Contains("TransferFrom") || line.Contains("approve") || line.Contains("Approve"))) return true;
			return false;
		}

		static string contractadd = string.Empty;
		static void _func()
		{
			if(!_isValidContract(contractadd))
			{
				print("@3The contract you provided is invalid!\n");
				return;
			}
			try
			{
				var json = new WebClient().DownloadString(
					"https://api.bscscan.com/api?module=contract&action=getsourcecode&address=" + contractadd + "&apikey=NQ89E3SU8ZHZFESTE3CJ3S8E4PZ1R4MXQG");
				dynamic deserializedProduct = JsonConvert.DeserializeObject<dynamic>(json);

				if (deserializedProduct["result"][0]["SourceCode"].ToString().Length < 10)
				{
					print("@3The provided contract is not verified! You should NOT buy this token.\n");
					return;
				}
				print("\n@2==========================================================\n\n");
				string path = Directory.GetCurrentDirectory() + @"\temp.txt";
				File.WriteAllText(path, deserializedProduct["result"][0]["SourceCode"].ToString());
				string[] lines = System.IO.File.ReadAllLines(path);

				Contract contract = new Contract();

				bool in_function = false, has_ac = false, in_comment = false, found_burnliq = false, tampered_hny_fnc = false,
					identifier_first_hny = false, source_found_router = false;
				ushort total_lines = 0, contract_lines = 0, count_files = 0, honeypot_score = 0;

				// CHECK TOKEN NAME
				contract.tokenName = deserializedProduct["result"][0]["ContractName"];
				print("@2Token: @1" + contract.tokenName + "\n");

				// CHECK COMPILER VERSION
				contract.compilerVersion = deserializedProduct["result"][0]["CompilerVersion"];
				if (contract.compilerVersion.StartsWith("v0.5") || contract.compilerVersion.StartsWith("v0.4") || contract.compilerVersion.StartsWith("0.3") ||
					contract.compilerVersion.StartsWith("v0.2") || contract.compilerVersion.StartsWith("v0.1"))
				{
					print("@2Compiler version: @1" + contract.compilerVersion + " @3(LOW COMPILER VERSION)\n");
					honeypot_score += 25;
				}
				else if (contract.compilerVersion.StartsWith("v0.6")) print("@2Compiler version: @1" + contract.compilerVersion + " @5(MID COMPILER VERSION)\n");
				else print("@2Compiler version: @1" + contract.compilerVersion + " @4(NEW COMPILER VERSION)\n");

				// CHECK OPTIMIZATIONS
				contract.optimizationsUsed = deserializedProduct["result"][0]["OptimizationUsed"];
				print("@2Optimization Used: @1" + (contract.optimizationsUsed == 1 ? "true" : "false") + "\n");

				// CHECK LICENSETYPE
				contract.licenseType = deserializedProduct["result"][0]["LicenseType"];
				print("@2License Type: @1" + contract.licenseType + "\n");

				foreach(string line in lines)
				{
					total_lines++;
					string cpy = line;
					// READING COMMENTS

					cpy = line.Replace(" ", string.Empty);
					if (cpy.StartsWith("/*")) in_comment = true;
					if (cpy.Contains("*/") && !cpy.StartsWith("//"))
					{
						in_comment = false;
						continue;
					}
					if (!cpy.StartsWith("//") && in_comment == false) contract_lines++;
					cpy = line;
					//

					// READING FUNCTIONS
					if (!in_function)
					{
						if (line.Contains("function ") && line.Contains("(") && !line.Contains(";"))
						{
							contract.functions[contract.at_func] = new Contract.Function();
							for (int i = 0; i < 200; i++) contract.functions[contract.at_func].lines[i] = string.Empty;
							in_function = true;
							contract.functions[contract.at_func].functionName = _getFunctionName(cpy);
							contract.functions[contract.at_func].lines[contract.functions[contract.at_func].at_line] = line;
							contract.functions[contract.at_func].at_line++;
							if(contract.functions[contract.at_func].functionName.Contains("lock") && 
								!contract.functions[contract.at_func].functionName.Contains("unlock") && 
								!contract.functions[contract.at_func].functionName.Contains("Unlock"))
							{
								print("@4(!) Locking liquidity function found! '@1" + line + "@4'\n");
							}
							else if (contract.functions[contract.at_func].functionName.Contains("burn"))
							{
								print("@4(!) Burning liquidity function found! '@1" + line + "@4'\n");
								found_burnliq = true;
							}
						}
					}
					else
					{
						// CONTRACT: 0x0b04c09071e14a6108883318391ff3fcf5ac3389
						if (_isHoneypotFunction(contract.functions[contract.at_func].functionName))
						{
							if(line.Contains("if(") || line.Contains("if ("))
							{
								if((_stringContainsAddress(line) || line.Contains("==") || line.Contains("!=")) && 
									!line.Contains("== 0)") && !line.Contains("== 0 ") && !line.Contains("!= 0)") && !line.Contains("!= 0 "))
								{
									if (!(lines[total_lines].Contains("fee") || lines[total_lines].Contains("Fee") ||
										lines[total_lines + 1].Contains("fee") || lines[total_lines + 1].Contains("Fee") ||
										lines[total_lines + 2].Contains("fee") || lines[total_lines + 2].Contains("Fee")) && !in_comment &&
										!lines[total_lines].Contains("//") && !lines[total_lines + 1].Contains("//") && !lines[total_lines + 2].Contains("//"))
									{
										print("@3(!) Function '@1" + contract.functions[contract.at_func].functionName + "@3' might be tampered! ('@1" + line + "@3')\n");
										tampered_hny_fnc = true;
									}
								}
							}
						}
						contract.functions[contract.at_func].lines[contract.functions[contract.at_func].at_line] = line;
						contract.functions[contract.at_func].at_line++;
						if (!has_ac)
						{
							if (line.Contains("{"))
							{
								has_ac = true;
							}
							if (line.Contains("}") && !has_ac)
							{
								in_function = false;
								contract.functions[contract.at_func].at_line = 0;
								contract.at_func++;
							}
						}
						else
						{
							if (line.Contains("}")) has_ac = false;
						}
					}
					//

					// READING ADDRESSES
					if(_stringContainsAddress(line))
					{
						if ((line.Contains("owner") || line.Contains("dev")) && line.Contains("= ") && !line.Contains("=="))
						{
							print("@3(!) Owner might be set from the source code! ('@1" + line + "@3')\n");
						}
						else print("@5(!) Found an address (line @1" + total_lines + "@5): '@1" + line + "@5'\n");
					}
					else if(line.Contains("address public new"))
					{
						print("@3(!) Found '@1" + line + "@3' in the source code!\n");
						identifier_first_hny = true;
					}

					// READING FILES
					if (line.Contains("pragma solidity") && !in_comment && !line.StartsWith("//")) count_files++;
					//

					// SEARCHING FOR ROUTER
					if (line.Contains("router") || line.Contains("Router")) source_found_router = true;
				}
				print("@2Total lines: @1" + total_lines + "\n");

				print("@2Contract lines: ");
				if (contract_lines < 250) print("@3" + contract_lines + "\n");
				else if ((contract_lines >= 250 && contract_lines < 400) || (total_lines - contract_lines) > contract_lines) print("@5" + contract_lines + "\n");
				else print("@4" + contract_lines + "\n");

				print("@2Commented lines: @1" + (total_lines - contract_lines) + "\n");

				print("@2This contract is made out of @5" + count_files + " @2files.\n\n");

				if (contract_lines < (total_lines - contract_lines)) honeypot_score += 20;

				// HONEYPOT PATTERNS
				if (contract_lines < 180 && identifier_first_hny == true && tampered_hny_fnc == true && (contract.compilerVersion.Contains("0.5") || 
					contract.compilerVersion.Contains("0.4") || contract.compilerVersion.Contains("0.3") || contract.compilerVersion.Contains("0.2") ||
					contract.compilerVersion.Contains("0.1")))
				{
					print("@3(!) This source code is using the 'INSUFFICIENT_OUTPUT_AMOUNT' exploit!\n");
					honeypot_score = 100;
				}
				else if((contract_lines < (total_lines - contract_lines) && found_burnliq && count_files == 1 && contract.compilerVersion.Contains("0.6")) || 
					tampered_hny_fnc == true)
				{
					print("@3(!) This source code has a function tampered or is using the 'CANNOT_ESTIMATE_GAS' exploit!\n");
					honeypot_score = 100;
				}
				else if(honeypot_score == 0 || (total_lines - (total_lines - contract_lines) > 500 && source_found_router && count_files == 1 && 
					contract.compilerVersion.Contains("0.6.12")))
				{
					print("@4(!) This source code looks legit!\n");
					honeypot_score = 0;
				}

				print("@2Honeypot Chance: ");
				if (honeypot_score < 5) print("@4" + honeypot_score + "%\n");
				else if (honeypot_score > 5 && honeypot_score <= 45) print("@5" + honeypot_score + "%\n");
				else print("@3" + honeypot_score + "% (!)\n");

				print("\n@6Source code copied at: " + path + "\n\n");
				print(contract.functions.Count() +"\n");
				print("@2==========================================================\n\n");
			}
			catch(Exception e)
			{
				print(e.ToString() + "\n");
			}
		}

		static void print(string message)
		{
			bool amp = false;
			for(int i = 0; i < message.Length; i++)
			{
				bool hold = false;
				if (!amp)
				{
					if (message[i].Equals('@'))
					{
						amp = true;
						hold = true;
					}
				}
				else
				{
					switch (message[i] - 48)
					{
						case 1:
							{
								Console.ForegroundColor = ConsoleColor.White;
								break;
							}
						case 2:
							{
								Console.ForegroundColor = ConsoleColor.Blue;
								break;
							}
						case 3:
							{
								Console.ForegroundColor = ConsoleColor.Red;
								break;
							}
						case 4:
							{
								Console.ForegroundColor = ConsoleColor.Green;
								break;
							}
						case 5:
							{
								Console.ForegroundColor = ConsoleColor.Yellow;
								break;
							}
						case 6:
							{
								Console.ForegroundColor = ConsoleColor.DarkYellow;
								break;
							}
					}
					amp = false;
					hold = true;
				}
				if (!hold) Console.Write(message[i]);
			}
			Console.ForegroundColor = ConsoleColor.White;
		}

		static void Main(string[] args)
		{
			Console.Title = "f0X checker";
			/*
			string connetionString;
			MySqlConnection con;
			connetionString = @"Server=sql11.freesqldatabase.com,3306;Database=XXX;User Id=XXX;Password=XXX;";
			Console.WriteLine("Attempting to connect to SQL...");
			con = new MySqlConnection(connetionString);
			try
			{
				con.Open();
				print("@4Successfully connected to SQL. Checking your license...");

				ManagementObject dsk = new ManagementObject(
					@"win32_logicaldisk.deviceid=""" + "C" + @":""");
				dsk.Get();
				string volumeSerial = dsk["VolumeSerialNumber"].ToString();
				var s = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_OperatingSystem");
				var obj = s.Get().Cast<ManagementObject>().First();
				var id = obj["SerialNumber"].ToString();

				string query = "SELECT * FROM licenses";
				MySqlCommand cmd = new MySqlCommand(query, con);
				MySqlDataReader rdr = cmd.ExecuteReader();
				bool found = false;
				while (rdr.Read())
				{
					if (rdr.GetString(1).Equals(id + "-" + volumeSerial) && rdr.GetInt32(3) == 1)
					{
						print("@4   OK!\n");
						found = true;
						break;
					}
				}
				if (!found)
				{
					print("\n@3Unable to find your license!");
					Console.Read(); return;
				}
			}
			catch (Exception e)
			{
				print("@3" + e.Message + "\n");
				Console.Read();
				return;
			}
			Console.Clear();*/
			do
			{
				print("@2Awaiting contract: ");
				contractadd = Console.ReadLine();
				_func();
			}
			while (!contractadd.Equals("0x0"));
		}
	}
}
