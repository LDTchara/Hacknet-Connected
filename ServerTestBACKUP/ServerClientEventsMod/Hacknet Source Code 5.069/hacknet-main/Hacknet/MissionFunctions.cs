using System;
using System.Collections.Generic;
using Hacknet.Factions;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;

namespace Hacknet;

public static class MissionFunctions
{
	private static OS os;

	public static Action<string> ReportErrorInCommand;

	private static void assertOS()
	{
		os = OS.currentInstance;
	}

	public static void runCommand(int value, string name)
	{
		assertOS();
		if (name.ToLower().Trim() == "none")
		{
			return;
		}
		if (name.Equals("addRank"))
		{
			if (!OS.TestingPassOnly || os.currentFaction != null)
			{
				os.currentFaction.addValue(value, os);
				string subject = LocaleTerms.Loc("Contract Successful");
				string body = string.Format(Utils.readEntireFile("Content/LocPost/MissionCompleteEmail.txt"), os.currentFaction.getRank(), os.currentFaction.getMaxRank(), os.currentFaction.name);
				string sender = os.currentFaction.name + " ReplyBot";
				string mail = MailServer.generateEmail(subject, body, sender);
				MailServer mailServer = (MailServer)os.netMap.mailServer.getDaemon(typeof(MailServer));
				mailServer.addMail(mail, os.defaultUser.name);
			}
			else if (OS.DEBUG_COMMANDS && os.currentFaction == null)
			{
				os.write("----------");
				os.write("----------");
				os.write("ERROR IN FUNCTION 'addRank'");
				os.write("Player is not assigned to a faction, so rank cannot be added!");
				os.write("Make sure you have assigned a player a faction with the 'SetFaction' function before using this!");
				os.write("----------");
				os.write("----------");
			}
			return;
		}
		if (name.Equals("addRankSilent"))
		{
			if (!OS.TestingPassOnly || os.currentFaction != null)
			{
				os.currentFaction.addValue(value, os);
			}
			return;
		}
		if (name.StartsWith("addFlags:"))
		{
			string text = name.Substring("addFlags:".Length);
			string[] array = text.Split(Utils.commaDelim, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < array.Length; i++)
			{
				os.Flags.AddFlag(array[i]);
			}
			if (os.currentFaction is CustomFaction customFaction)
			{
				customFaction.CheckForAllCustomActionsToRun(os);
			}
			return;
		}
		if (name.StartsWith("removeFlags:"))
		{
			string text = name.Substring("removeFlags:".Length);
			string[] array = text.Split(Utils.commaDelim, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < array.Length; i++)
			{
				if (os.Flags.HasFlag(array[i]))
				{
					os.Flags.RemoveFlag(array[i]);
				}
			}
			if (os.currentFaction is CustomFaction customFaction2)
			{
				customFaction2.CheckForAllCustomActionsToRun(os);
			}
			return;
		}
		if (name.StartsWith("setFaction:"))
		{
			string text2 = name.Substring("setFaction:".Length);
			bool flag = false;
			foreach (KeyValuePair<string, Faction> faction in os.allFactions.factions)
			{
				if (faction.Value.idName.ToLower() == text2.ToLower())
				{
					os.allFactions.setCurrentFaction(text2, os);
					flag = true;
					break;
				}
			}
			if (flag || !OS.TestingPassOnly)
			{
				return;
			}
			throw new NullReferenceException("Faction " + text2 + "not found for setFaction action!");
		}
		if (name.StartsWith("loadConditionalActions:"))
		{
			string filepath = name.Substring("loadConditionalActions:".Length);
			RunnableConditionalActions.LoadIntoOS(filepath, os);
			return;
		}
		if (name.Equals("triggerThemeHackRevenge"))
		{
			os.delayer.Post(ActionDelayer.Wait(5.0), delegate
			{
				string subject2 = LocaleTerms.Loc("Are you Kidding me?");
				string body3 = Utils.readEntireFile("Content/LocPost/NaixEmail.txt");
				string sender2 = "naix@jmail.com";
				string mail3 = MailServer.generateEmail(subject2, body3, sender2);
				MailServer mailServer3 = (MailServer)os.netMap.mailServer.getDaemon(typeof(MailServer));
				mailServer3.addMail(mail3, os.defaultUser.name);
				os.delayer.Post(ActionDelayer.Wait(24.0), delegate
				{
					try
					{
						HackerScriptExecuter.runScript("HackerScripts/ThemeHack.txt", os);
					}
					catch (Exception ex2)
					{
						if (Settings.recoverFromErrorsSilently)
						{
							os.write("CAUTION: UNSYNDICATED OUTSIDE CONNECTION ATTEMPT");
							os.write("RECOVERED FROM CONNECTION SUBTERFUGE SUCCESSFULLY");
							Console.WriteLine("Critical error loading hacker script - aborting");
							return;
						}
						throw ex2;
					}
				});
			});
			return;
		}
		if (name.Equals("changeSong"))
		{
			switch (value)
			{
			default:
				MusicManager.transitionToSong("Music\\Revolve");
				break;
			case 2:
				MusicManager.transitionToSong("Music\\The_Quickening");
				break;
			case 3:
				MusicManager.transitionToSong("Music\\TheAlgorithm");
				break;
			case 4:
				MusicManager.transitionToSong("Music\\Ryan3");
				break;
			case 5:
				MusicManager.transitionToSong("Music\\Bit(Ending)");
				break;
			case 6:
				MusicManager.transitionToSong("Music\\Rico_Puestel-Roja_Drifts_By");
				break;
			case 7:
				MusicManager.transitionToSong("Music\\out_run_the_wolves");
				break;
			case 8:
				MusicManager.transitionToSong("Music\\Irritations");
				break;
			case 9:
				MusicManager.transitionToSong("Music\\Broken_Boy");
				break;
			case 10:
				MusicManager.transitionToSong("Music\\Ryan10");
				break;
			case 11:
				MusicManager.transitionToSong("Music\\tetrameth");
				break;
			}
			return;
		}
		if (name.Equals("entropyEndMissionSetup"))
		{
			runCommand(3, "changeSong");
			Computer computer = findComp("corp0#IS");
			Computer computer2 = findComp("corp0#MF");
			Computer computer3 = findComp("corp0#BU");
			FileEntry fileEntry = new FileEntry(Computer.generateBinaryString(5000), "HacknetOS.rar");
			FileEntry fileEntry2 = new FileEntry(Computer.generateBinaryString(4000), "HacknetOS_Data.xnb");
			FileEntry fileEntry3 = new FileEntry(Computer.generateBinaryString(4000), "HacknetOS_Content.xnb");
			Folder folder = computer.files.root.folders[2];
			folder.files.Add(fileEntry);
			folder.files.Add(fileEntry2);
			folder.files.Add(fileEntry3);
			folder = computer2.files.root.folders[2];
			folder.files.Add(fileEntry);
			folder.files.Add(fileEntry2);
			folder.files.Add(fileEntry3);
			fileEntry = new FileEntry(fileEntry.data, fileEntry.name + "_backup");
			fileEntry2 = new FileEntry(fileEntry2.data, fileEntry2.name + "_backup");
			fileEntry3 = new FileEntry(fileEntry3.data, fileEntry3.name + "_backup");
			folder = computer3.files.root.folders[2];
			folder.files.Add(fileEntry);
			folder.files.Add(fileEntry2);
			folder.files.Add(fileEntry3);
			computer.traceTime = Computer.BASE_TRACE_TIME * 7.5f;
			computer3.traceTime = Computer.BASE_TRACE_TIME * 7.5f;
			computer2.traceTime = Computer.BASE_TRACE_TIME * 7.5f;
			computer2.portsNeededForCrack = 3;
			computer.portsNeededForCrack = 2;
			computer3.portsNeededForCrack = 2;
			Computer computer4 = findComp("entropy01");
			Folder folder2 = computer4.files.root.folders[2];
			folder2.files.Add(new FileEntry(PortExploits.crackExeData[25], "SMTPoverflow.exe"));
			folder2.files.Add(new FileEntry(PortExploits.crackExeData[80], "WebServerWorm.exe"));
			return;
		}
		if (name.Equals("entropyAddSMTPCrack"))
		{
			Computer computer4 = findComp("entropy01");
			Folder folder2 = computer4.files.root.folders[2];
			bool flag2 = false;
			for (int i = 0; i < folder2.files.Count; i++)
			{
				if (folder2.files[i].data == PortExploits.crackExeData[25] || folder2.files[i].name == "SMTPoverflow.exe")
				{
					flag2 = true;
				}
			}
			if (!flag2)
			{
				folder2.files.Add(new FileEntry(PortExploits.crackExeData[25], Utils.GetNonRepeatingFilename("SMTPoverflow", ".exe", folder2)));
			}
			os.Flags.AddFlag("ThemeHackTransitionAssetsAdded");
			return;
		}
		if (name.Equals("transitionToBitMissions"))
		{
			if (Settings.isDemoMode)
			{
				runCommand(6, "changeSong");
				if (Settings.isPressBuildDemo)
				{
					ComputerLoader.loadMission("Content/Missions/Demo/PressBuild/DemoMission01.xml");
				}
				else
				{
					ComputerLoader.loadMission("Content/Missions/Demo/AvconDemo.xml");
				}
			}
			else
			{
				ComputerLoader.loadMission("Content/Missions/BitMission0.xml");
			}
			return;
		}
		if (name.Equals("entropySendCSECInvite"))
		{
			os.delayer.Post(ActionDelayer.Wait(6.0), delegate
			{
				ComputerLoader.loadMission("Content/Missions/MainHub/Intro/Intro01.xml");
			});
			return;
		}
		if (name.Equals("hubBitSetComplete01"))
		{
			os.delayer.Post(ActionDelayer.Wait(4.0), delegate
			{
				runCommand(1, "addRank");
			});
			runCommand(3, "changeSong");
			os.Flags.AddFlag("csecBitSet01Complete");
			return;
		}
		if (name.Equals("enTechEnableOfflineBackup"))
		{
			Computer computer5 = Programs.getComputer(os, "EnTechOfflineBackup");
			os.Flags.AddFlag("VaporSequencerEnabled");
			Computer computer4 = findComp("mainHubAssets");
			Folder folder2 = computer4.files.root.searchForFolder("bin");
			Folder folder3 = folder2.searchForFolder("Sequencer");
			if (folder3 == null)
			{
				folder3 = new Folder("Sequencer");
				folder2.folders.Add(folder3);
			}
			if (folder3.searchForFile("Sequencer.exe") == null)
			{
				folder3.files.Add(new FileEntry(PortExploits.crackExeData[17], "Sequencer.exe"));
			}
			return;
		}
		if (name.Equals("rudeNaixResponse"))
		{
			AchievementsManager.Unlock("rude_response");
			return;
		}
		if (name.Equals("assignPlayerToHubServerFaction"))
		{
			os.allFactions.setCurrentFaction("hub", os);
			Computer computer6 = Programs.getComputer(os, "mainHub");
			MissionHubServer missionHubServer = (MissionHubServer)computer6.getDaemon(typeof(MissionHubServer));
			UserDetail userDetail = new UserDetail(os.defaultUser.name, "reptile", 3);
			computer6.addNewUser(computer6.ip, userDetail);
			missionHubServer.addUser(userDetail);
			os.homeNodeID = "mainHub";
			os.homeAssetServerID = "mainHubAssets";
			runCommand(3, "changeSong");
			os.Flags.AddFlag("CSEC_Member");
			AchievementsManager.Unlock("progress_csec");
			if (os.HasLoadedDLCContent && Settings.EnableDLC && !os.Flags.HasFlag("dlc_complete"))
			{
				missionHubServer.AddMissionToListings("Content/DLC/Missions/BaseGameConnectors/Missions/CSEC_DLCConnectorIntro.xml", 1);
			}
			return;
		}
		if (name.Equals("assignPlayerToEntropyFaction"))
		{
			runCommand(6, "changeSong");
			os.homeNodeID = "entropy00";
			os.homeAssetServerID = "entropy01";
			AchievementsManager.Unlock("progress_entropy");
			return;
		}
		if (name.Equals("assignPlayerToLelzSec"))
		{
			os.homeNodeID = "lelzSecHub";
			os.homeAssetServerID = "lelzSecHub";
			os.Flags.AddFlag("LelzSec_Member");
			AchievementsManager.Unlock("progress_lelz");
			return;
		}
		if (name.Equals("lelzSecVictory"))
		{
			AchievementsManager.Unlock("secret_path_complete");
			return;
		}
		if (name.Equals("demoFinalMissionEnd"))
		{
			os.exes.Clear();
			PostProcessor.EndingSequenceFlashOutActive = true;
			PostProcessor.EndingSequenceFlashOutPercentageComplete = 1f;
			MusicManager.stop();
			os.delayer.Post(ActionDelayer.Wait(0.2), delegate
			{
				os.content.Load<SoundEffect>("Music/Ambient/spiral_gauge_down").Play();
			});
			os.delayer.Post(ActionDelayer.Wait(3.0), delegate
			{
				PostProcessor.dangerModeEnabled = false;
				PostProcessor.dangerModePercentComplete = 0f;
				os.ExitScreen();
				os.ScreenManager.AddScreen(new DemoEndScreen());
			});
			return;
		}
		if (name.Equals("demoFinalMissionEndDLC"))
		{
			if (Settings.isDemoMode)
			{
				os.exes.Clear();
				PostProcessor.EndingSequenceFlashOutActive = true;
				PostProcessor.EndingSequenceFlashOutPercentageComplete = 1f;
				MusicManager.stop();
				os.delayer.Post(ActionDelayer.Wait(0.0), delegate
				{
					os.content.Load<SoundEffect>("SFX/BrightFlash").Play();
				});
				os.delayer.Post(ActionDelayer.Wait(0.4), delegate
				{
					os.content.Load<SoundEffect>("SFX/TraceKill").Play();
				});
				os.delayer.Post(ActionDelayer.Wait(1.6), delegate
				{
					MusicManager.playSongImmediatley("DLC/Music/DreamHead");
					PostProcessor.dangerModeEnabled = false;
					PostProcessor.dangerModePercentComplete = 0f;
					os.ScreenManager.AddScreen(new DemoEndScreen
					{
						StopsMusic = false,
						IsDLCDemoScreen = true
					});
					os.ExitScreen();
				});
			}
			return;
		}
		if (name.Equals("demoFinalMissionStart"))
		{
			os.Flags.AddFlag("DemoSequencerEnabled");
			MusicManager.transitionToSong("Music/Ambient/dark_drone_008");
			return;
		}
		if (name.Equals("CSECTesterGameWorldSetup"))
		{
			for (int i = 0; i < PortExploits.services.Count && i < 4; i++)
			{
				os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[PortExploits.portNums[i]], PortExploits.cracks[PortExploits.portNums[i]]));
			}
			for (int i = 0; i < 4; i++)
			{
				Computer computer5 = new Computer("DebugShell" + i, NetworkMap.generateRandomIP(), os.netMap.getRandomPosition(), 0, 2, os);
				computer5.adminIP = os.thisComputer.adminIP;
				os.netMap.nodes.Add(computer5);
				os.netMap.discoverNode(computer5);
			}
			os.delayer.Post(ActionDelayer.Wait(0.2), delegate
			{
				os.allFactions.setCurrentFaction("entropy", os);
				os.currentMission = null;
				os.netMap.discoverNode(Programs.getComputer(os, "entropy00"));
				os.netMap.discoverNode(Programs.getComputer(os, "entropy01"));
			});
			return;
		}
		if (name.Equals("EntropyFastFowardSetup"))
		{
			os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[22], PortExploits.cracks[22]));
			os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[21], PortExploits.cracks[21]));
			for (int i = 0; i < 3; i++)
			{
				Computer computer5 = new Computer("DebugShell" + i, NetworkMap.generateRandomIP(), os.netMap.getRandomPosition(), 0, 2, os);
				computer5.adminIP = os.thisComputer.adminIP;
				os.netMap.nodes.Add(computer5);
				os.netMap.discoverNode(computer5);
			}
			os.delayer.Post(ActionDelayer.Wait(0.2), delegate
			{
				os.allFactions.setCurrentFaction("entropy", os);
				os.currentMission = null;
				os.netMap.discoverNode(Programs.getComputer(os, "entropy00"));
				os.netMap.discoverNode(Programs.getComputer(os, "entropy01"));
				Computer computer11 = Programs.getComputer(os, "entropy01");
				UserDetail value3 = computer11.users[0];
				value3.known = true;
				computer11.users[0] = value3;
				os.allFactions.factions[os.allFactions.currentFaction].playerValue = 2;
				os.delayer.Post(ActionDelayer.Wait(0.2), delegate
				{
					os.Flags.AddFlag("eosPathStarted");
					ComputerLoader.loadMission("Content/Missions/Entropy/StartingSet/eosMissions/eosIntroDelayer.xml");
				});
			});
			return;
		}
		if (name.Equals("CSECFastFowardSetup"))
		{
			os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[22], PortExploits.cracks[22]));
			os.thisComputer.files.root.folders[2].files.Add(new FileEntry(PortExploits.crackExeData[21], PortExploits.cracks[21]));
			for (int i = 0; i < 3; i++)
			{
				Computer computer5 = new Computer("DebugShell" + i, NetworkMap.generateRandomIP(), os.netMap.getRandomPosition(), 0, 2, os);
				computer5.adminIP = os.thisComputer.adminIP;
				os.netMap.nodes.Add(computer5);
				os.netMap.discoverNode(computer5);
			}
			os.delayer.Post(ActionDelayer.Wait(0.2), delegate
			{
				runCommand(0, "assignPlayerToHubServerFaction");
				os.currentMission = null;
				os.netMap.discoverNode(Programs.getComputer(os, "mainHub"));
				os.netMap.discoverNode(Programs.getComputer(os, "mainHubAssets"));
				Computer computer10 = Programs.getComputer(os, "mainHubAssets");
				UserDetail value2 = computer10.users[0];
				value2.known = true;
				computer10.users[0] = value2;
			});
			return;
		}
		if (name.Equals("csecAddTraceKill"))
		{
			Computer computer4 = findComp("mainHubAssets");
			Folder folder2 = computer4.files.root.searchForFolder("bin");
			Folder folder3 = folder2.searchForFolder("TK");
			if (folder3 == null)
			{
				folder3 = new Folder("TK");
				folder2.folders.Add(folder3);
			}
			folder3.files.Add(new FileEntry(FileEncrypter.EncryptString(PortExploits.crackExeData[12], "Vapor Trick Enc.", "NULL", "dx122DX", ".exe"), Utils.GetNonRepeatingFilename("TraceKill", ".dec", folder3)));
			os.Flags.AddFlag("bitPathStarted");
			runCommand(10, "changeSong");
			return;
		}
		if (name.Equals("junebugComplete"))
		{
			Computer computer5 = Programs.getComputer(os, "pacemaker01");
			if (computer5 != null)
			{
				((HeartMonitorDaemon)computer5.getDaemon(typeof(HeartMonitorDaemon)))?.ForceStopBeepSustainSound();
			}
			runCommand(1, "addRank");
			return;
		}
		if (name.Equals("eosIntroMissionSetup"))
		{
			Computer computer4 = findComp("entropy01");
			Folder folder2 = computer4.files.root.searchForFolder("bin");
			folder2.files.Add(new FileEntry(PortExploits.crackExeData[13], "eosDeviceScan.exe"));
			os.delayer.Post(ActionDelayer.Wait(8.0), delegate
			{
				string body2 = Utils.readEntireFile("Content/Post/eosScannerMail.txt");
				string text4 = Utils.readEntireFile("Content/LocPost/eOSNote.txt");
				string text5 = "note#%#" + LocaleTerms.Loc("eOS Security Basics") + "#%#" + text4;
				string mail2 = MailServer.generateEmail("Fwd: eOS Stuff", body2, "vtfx", new List<string>(new string[1] { text5 }));
				MailServer mailServer2 = (MailServer)os.netMap.mailServer.getDaemon(typeof(MailServer));
				mailServer2.addMail(mail2, os.defaultUser.name);
				os.saveGame();
			});
			runCommand(4, "changeSong");
			os.saveGame();
			return;
		}
		if (name.Equals("eosIntroEndFunc"))
		{
			runCommand(1, "addRank");
			Computer computer7 = findComp("entropy00");
			MissionListingServer missionListingServer = (MissionListingServer)computer7.getDaemon(typeof(MissionListingServer));
			List<ActiveMission> branchMissions = os.branchMissions;
			ActiveMission m = (ActiveMission)ComputerLoader.readMission("Content/Missions/Entropy/StartingSet/eosMissions/eosAddedMission.xml");
			missionListingServer.addMisison(m);
			os.branchMissions = branchMissions;
			return;
		}
		if (name.Equals("changeSongDLC"))
		{
			switch (value)
			{
			default:
				MusicManager.transitionToSong("DLC\\Music\\Remi2");
				break;
			case 2:
				MusicManager.transitionToSong("DLC\\Music\\snidelyWhiplash");
				break;
			case 3:
				MusicManager.transitionToSong("DLC\\Music\\Slow_Motion");
				break;
			case 4:
				MusicManager.transitionToSong("DLC\\Music\\World_Chase");
				break;
			case 5:
				MusicManager.transitionToSong("DLC\\Music\\HOME_Resonance");
				break;
			case 6:
				MusicManager.transitionToSong("DLC\\Music\\Remi_Finale");
				break;
			case 7:
				MusicManager.transitionToSong("DLC\\Music\\RemiDrone");
				break;
			case 8:
				MusicManager.transitionToSong("DLC\\Music\\DreamHead");
				break;
			case 9:
				MusicManager.transitionToSong("DLC\\Music\\Userspacelike");
				break;
			case 10:
				MusicManager.transitionToSong("DLC\\Music\\CrashTrack");
				break;
			}
			return;
		}
		if (name.Equals("scanAndStartDLCVenganceHack"))
		{
			Computer computer5 = findComp("dAttackTarget");
			if (computer5 == null)
			{
				return;
			}
			Folder folder4 = computer5.files.root.searchForFolder("log");
			bool flag3 = false;
			for (int i = 0; i < folder4.files.Count; i++)
			{
				if (folder4.files[i].data.Contains(os.thisComputer.ip))
				{
					SARunFunction sARunFunction = new SARunFunction();
					sARunFunction.DelayHost = "dhs";
					sARunFunction.FunctionName = "triggerDLCHackRevenge";
					sARunFunction.FunctionValue = 1;
					SARunFunction action = sARunFunction;
					Computer computer8 = Programs.getComputer(os, "dhs");
					DLCHubServer dLCHubServer = (DLCHubServer)computer8.getDaemon(typeof(DLCHubServer));
					dLCHubServer.DelayedActions.AddAction(action, 16f);
					break;
				}
			}
			if (!flag3)
			{
				runCommand(4, "changeSongDLC");
			}
			return;
		}
		if (name.Equals("triggerDLCHackRevenge"))
		{
			try
			{
				HackerScriptExecuter.runScript("DLC/ActionScripts/Hackers/SystemHack.txt", os);
				return;
			}
			catch (Exception ex)
			{
				if (Settings.recoverFromErrorsSilently)
				{
					os.write("CAUTION: UNSYNDICATED OUTSIDE CONNECTION ATTEMPT");
					os.write("RECOVERED FROM CONNECTION SUBTERFUGE SUCCESSFULLY");
					Console.WriteLine("Critical error loading hacker script - aborting\r\n" + Utils.GenerateReportFromException(ex));
					return;
				}
				throw ex;
			}
		}
		if (name.Equals("activateAircraftStatusOverlay"))
		{
			os.AircraftInfoOverlay.Activate();
			os.AircraftInfoOverlay.IsMonitoringDLCEndingCases = true;
			return;
		}
		if (name.Equals("activateAircraftStatusOverlayLabyrinthsMonitoring"))
		{
			os.AircraftInfoOverlay.IsMonitoringDLCEndingCases = true;
			return;
		}
		if (name.Equals("deActivateAircraftStatusOverlay"))
		{
			os.AircraftInfoOverlay.IsActive = false;
			os.AircraftInfoOverlay.IsMonitoringDLCEndingCases = false;
			os.Flags.AddFlag("AircraftInfoOverlayDeactivated");
			return;
		}
		if (name.Equals("defAttackAircraft"))
		{
			Computer computer9 = Programs.getComputer(os, "dair_crash");
			Folder folder4 = computer9.files.root.searchForFolder("FlightSystems");
			for (int i = 0; i < folder4.files.Count; i++)
			{
				if (folder4.files[i].name == "747FlightOps.dll")
				{
					folder4.files.RemoveAt(i);
					break;
				}
			}
			AircraftDaemon aircraftDaemon = (AircraftDaemon)computer9.getDaemon(typeof(AircraftDaemon));
			aircraftDaemon.StartReloadFirmware();
			if (!os.AircraftInfoOverlay.IsActive)
			{
				os.AircraftInfoOverlay.Activate();
				os.AircraftInfoOverlay.IsMonitoringDLCEndingCases = true;
			}
			return;
		}
		if (name.Equals("playAirlineCrashSongSequence"))
		{
			MusicManager.playSongImmediatley("DLC\\Music\\Remi_Finale");
			MediaPlayer.IsRepeating = false;
			return;
		}
		if (name.Equals("flashUI"))
		{
			os.warningFlash();
			return;
		}
		if (name.Equals("addRankSilent"))
		{
			os.currentFaction.addValue(value, os);
			return;
		}
		if (name.StartsWith("addRankFaction:"))
		{
			string text2 = name.Substring("addRankFaction:".Length);
			{
				foreach (KeyValuePair<string, Faction> faction2 in os.allFactions.factions)
				{
					if (faction2.Value.idName.ToLower() == text2.ToLower())
					{
						faction2.Value.addValue(value, os);
						break;
					}
				}
				return;
			}
		}
		if (name.StartsWith("setHubServer:"))
		{
			string homeNodeID = name.Substring("setHubServer:".Length);
			os.homeNodeID = homeNodeID;
		}
		else if (name.StartsWith("setAssetServer:"))
		{
			string homeNodeID = name.Substring("setAssetServer:".Length);
			os.homeAssetServerID = homeNodeID;
		}
		else if (name.StartsWith("playCustomSong:"))
		{
			string text3 = name.Substring("playCustomSong:".Length);
			text3 = Utils.GetFileLoadPrefix() + text3;
			if (text3.EndsWith(".ogg"))
			{
				text3 = text3.Substring(0, text3.Length - ".ogg".Length);
			}
			if (text3.StartsWith("Content"))
			{
				text3 = text3.Substring("Content/".Length);
			}
			else if (text3.StartsWith("Extensions"))
			{
				text3 = "../" + text3;
			}
			MusicManager.transitionToSong(text3);
		}
		else if (name.StartsWith("playCustomSongImmediatley:"))
		{
			string text3 = name.Substring("playCustomSongImmediatley:".Length);
			text3 = Utils.GetFileLoadPrefix() + text3;
			if (text3.EndsWith(".ogg"))
			{
				text3 = text3.Substring(0, text3.Length - ".ogg".Length);
			}
			if (text3.StartsWith("Content"))
			{
				text3 = text3.Substring("Content/".Length);
			}
			else if (text3.StartsWith("Extensions"))
			{
				text3 = "../" + text3;
			}
			MusicManager.playSongImmediatley(text3);
		}
		else
		{
			if (OS.TestingPassOnly && !string.IsNullOrWhiteSpace(name))
			{
				throw new FormatException("No Command Function " + name);
			}
			if (ReportErrorInCommand != null)
			{
				ReportErrorInCommand("No command found for \"" + name + "\" with value \"" + value + "\"");
			}
		}
	}

	private static void MediaPlayer_MediaStateChanged(object sender, EventArgs e)
	{
		throw new NotImplementedException();
	}

	private static Computer findComp(string target)
	{
		for (int i = 0; i < os.netMap.nodes.Count; i++)
		{
			if (os.netMap.nodes[i].idName.Equals(target))
			{
				return os.netMap.nodes[i];
			}
		}
		return null;
	}
}
