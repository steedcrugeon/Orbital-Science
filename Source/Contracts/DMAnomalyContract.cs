﻿#region license
/* DMagic Orbital Science - DMAnomalyContract
 * Class for generating anomaly science experiment contracts
 *
 * Copyright (c) 2014, David Grandy <david.grandy@gmail.com>
 * All rights reserved.
 * 
 * Redistribution and use in source and binary forms, with or without modification, 
 * are permitted provided that the following conditions are met:
 * 
 * 1. Redistributions of source code must retain the above copyright notice, 
 * this list of conditions and the following disclaimer.
 * 
 * 2. Redistributions in binary form must reproduce the above copyright notice, 
 * this list of conditions and the following disclaimer in the documentation and/or other materials 
 * provided with the distribution.
 * 
 * 3. Neither the name of the copyright holder nor the names of its contributors may be used 
 * to endorse or promote products derived from this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, 
 * INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE 
 * GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF 
 * LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT 
 * OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 *  
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Contracts;
using Contracts.Parameters;
using Contracts.Agents;
using DMagic.Parameters;
using DMagic.Scenario;

namespace DMagic.Contracts
{
	public class DMAnomalyContract: Contract
	{
		private DMCollectScience newParam;
		private DMScienceContainer DMScience;
		private List<DMScienceContainer> sciList = new List<DMScienceContainer>();
		private DMAnomalyParameter[] anomParams = new DMAnomalyParameter[4];
		private string body;
		private DMAnomalyObject targetAnomaly;
		private double lat, lon;
		private string cardNS, cardEW, hash;
		private int i = 0;
		private System.Random rand = DMUtils.rand;
		private System.Random r;
		private int latRand, lonRand;

		protected override bool Generate()
		{
			DMAnomalyContract[] anomContracts = ContractSystem.Instance.GetCurrentContracts<DMAnomalyContract>();
			int offers = 0;
			int active = 0;
			int maxOffers = DMContractDefs.DMAnomaly.maxOffers;
			int maxActive = DMContractDefs.DMAnomaly.maxActive;

			for (int i = 0; i < anomContracts.Length; i++ )
			{
				DMAnomalyContract a = anomContracts[i];
				if (a.ContractState == State.Offered)
					offers++;
				else if (a.ContractState == State.Active)
					active++;
			}

			if (offers >= maxOffers)
				return false;
			if (active >= maxActive)
				return false;

			//Make sure that the anomaly scanner is available
			if (!DMUtils.partAvailable(new List<string>(1) { "dmAnomScanner" }))
				return false;

			int reconLevelRequirement = 0;
			float levelLow = 0;
			float levelHigh = 1;

			switch(this.prestige)
			{
				case ContractPrestige.Trivial:
					reconLevelRequirement = DMContractDefs.DMAnomaly.TrivialReconLevelRequirement;
					levelLow = DMContractDefs.DMAnomaly.TrivialAnomalyLevel;
					levelHigh = DMContractDefs.DMAnomaly.SignificantAnomalyLevel;
					break;
				case ContractPrestige.Significant:
					reconLevelRequirement = DMContractDefs.DMAnomaly.SignificantReconLevelRequirement;
					levelLow = DMContractDefs.DMAnomaly.SignificantAnomalyLevel;
					levelHigh = DMContractDefs.DMAnomaly.ExceptionalAnomalyLevel;
					break;
				case ContractPrestige.Exceptional:
					reconLevelRequirement = DMContractDefs.DMAnomaly.ExceptionalReconLevelRequirement;
					levelLow = DMContractDefs.DMAnomaly.ExceptionalAnomalyLevel;
					levelHigh = 1f;
					break;
			}

			List<CelestialBody> customReachedBodies = ContractSystem.Instance.GetCompletedContracts<DMReconContract>().Where(c => (int)c.Prestige >= reconLevelRequirement).Select(c => c.Body).ToList();

			List<DMAnomalyStorage> anomalies = new List<DMAnomalyStorage>();

			for (int i = 0; i < customReachedBodies.Count; i++)
			{
				CelestialBody b = customReachedBodies[i];

				DMAnomalyStorage stor = DMAnomalyList.getAnomalyStorage(b.bodyName);

				if (stor == null)
					continue;

				if (stor.Level >= levelLow && stor.Level < levelHigh)
					anomalies.Add(stor);
			}

			var allAnom = anomalies.SelectMany(a => a.getAllAnomalies);

			var reducedAnom = allAnom.Where(a => a.Name != "KSC" && a.Name != "KSC2" && a.Name != "IslandAirfield");

			if (reducedAnom.Count() <= 0)
				return false;

			targetAnomaly = reducedAnom.ElementAt(rand.Next(0, reducedAnom.Count()));

			if (targetAnomaly == null)
				return false;

			body = targetAnomaly.Body.bodyName;

			r = new System.Random(this.MissionSeed);
			latRand = r.Next(-5, 5);
			lonRand = r.Next(-5, 5);

			hash = targetAnomaly.Name;
			lon = targetAnomaly.Lon;
			lat = targetAnomaly.Lat;
			cardNS = NSDirection(lat);
			cardEW = EWDirection(lon);

			//Assign primary anomaly contract parameter
			if ((newParam = DMAnomalyGenerator.fetchAnomalyParameter(targetAnomaly.Body, targetAnomaly)) == null)
				return false;

			sciList.AddRange(DMUtils.availableScience[DMScienceType.Anomaly.ToString()].Values);

			for (i = 0; i < 3; i++)
			{
				if (sciList.Count > 0)
				{
					DMScience = sciList[rand.Next(0, sciList.Count)];
					anomParams[i] = (DMAnomalyGenerator.fetchAnomalyParameter(targetAnomaly.Body, DMScience));
					sciList.Remove(DMScience);
				}
				else
					anomParams[i] = null;
			}

			this.AddParameter(newParam);

			float primaryLocationMod = GameVariables.Instance.ScoreSituation(DMUtils.convertSit(newParam.Situation), newParam.Body) * ((float)rand.Next(85, 116) / 100f);

			newParam.SetFunds(DMContractDefs.DMAnomaly.Funds.ParamReward * primaryLocationMod, DMContractDefs.DMAnomaly.Funds.ParamFailure * primaryLocationMod, targetAnomaly.Body);
			newParam.SetScience(DMContractDefs.DMAnomaly.Science.ParamReward * DMUtils.fixSubjectVal(newParam.Situation, 1f, targetAnomaly.Body), null);
			newParam.SetReputation(DMContractDefs.DMAnomaly.Reputation.ParamReward * primaryLocationMod, DMContractDefs.DMAnomaly.Reputation.ParamFailure * primaryLocationMod, null);

			//Add the science collection parent parameter
			DMCompleteParameter DMcp = new DMCompleteParameter(3, 1);
			this.AddParameter(DMcp);

			foreach (DMAnomalyParameter aP in anomParams)
			{
				if (aP != null)
				{
					if (aP.Container == null)
						continue;

					DMcp.addToSubParams(aP);
					float locationMod = GameVariables.Instance.ScoreSituation(DMUtils.convertSit(aP.Situation), targetAnomaly.Body) * ((float)rand.Next(85, 116) / 100f);
					aP.SetFunds((DMContractDefs.DMAnomaly.Funds.ParamReward / 2) * locationMod, (DMContractDefs.DMAnomaly.Funds.ParamFailure / 2) * locationMod, targetAnomaly.Body);
					aP.SetScience(aP.Container.Exp.baseValue * DMContractDefs.DMAnomaly.Science.SecondaryReward * DMUtils.fixSubjectVal(aP.Situation, 1f, targetAnomaly.Body), null);
					aP.SetReputation(DMContractDefs.DMAnomaly.Reputation.ParamReward * locationMod, DMContractDefs.DMAnomaly.Reputation.ParamFailure * locationMod, null);
				}
			}

			if (DMcp.ParameterCount < 2)
				return false;

			this.agent = AgentList.Instance.GetAgent("DMagic");

			if (this.agent == null)
				this.agent = AgentList.Instance.GetAgentRandom();

			base.SetExpiry(DMContractDefs.DMAnomaly.Expire.MinimumExpireDays, DMContractDefs.DMAnomaly.Expire.MaximumExpireDays);
			base.SetDeadlineYears(DMContractDefs.DMAnomaly.Expire.DeadlineYears * ((float)rand.Next(80, 121)) / 100f, targetAnomaly.Body);
			base.SetReputation(DMContractDefs.DMAnomaly.Reputation.BaseReward * primaryLocationMod, DMContractDefs.DMAnomaly.Reputation.BaseFailure * primaryLocationMod, null);
			base.SetScience(DMContractDefs.DMAnomaly.Science.BaseReward, null);
			base.SetFunds(DMContractDefs.DMAnomaly.Funds.BaseAdvance * primaryLocationMod, DMContractDefs.DMAnomaly.Funds.BaseReward * primaryLocationMod, DMContractDefs.DMAnomaly.Funds.BaseFailure * primaryLocationMod, targetAnomaly.Body);
			return true;
		}

		private double FudgedLat
		{
			get
			{
				if (HighLogic.LoadedSceneIsFlight)
					lat = targetAnomaly.Lat;
				return Math.Round(((double)latRand + lat) / 10d) * 10d;
			}
		}

		private double FudgedLon
		{
			get
			{
				if (HighLogic.LoadedSceneIsFlight)
					lon = targetAnomaly.Lon;
				return Math.Round(((double)lonRand + lon) / 10d) * 10d;
			}
		}

		private string NSDirection(double Lat)
		{
			if (Lat >= -90 && Lat < 0)
				return "South";
			if (Lat >= 0 && Lat <= 90)
				return "North";
			return "";
		}

		private string EWDirection(double Lon)
		{
			if (Lon >= -180 && Lon < 0)
				return "West";
			if (Lon >= 0 && Lon < 180)
				return "East";
			return "";
		}

		public override bool CanBeCancelled()
		{
			return true;
		}

		public override bool CanBeDeclined()
		{
			return true;
		}

		protected override string GetHashString()
		{
			return hash;
		}

		protected override string GetTitle()
		{
			if (targetAnomaly == null)
				return "Whoops. Something bad happened here...";

			return string.Format("Study the source of the anomalous readings coming from {0}'s surface", targetAnomaly.Body.displayName.LocalizeBodyName());
		}

		protected override string GetNotes()
		{
			return string.Format("Locate the anomalous signal coming from roughly {0}° {1} and {2}° {3}.", Math.Abs(FudgedLat), cardNS, Math.Abs(FudgedLon), cardEW);
		}

		protected override string GetDescription()
		{
			if (targetAnomaly == null)
				return "Whoops. Something bad happened here...";

			string story = DMContractDefs.DMAnomaly.backStory[rand.Next(0, DMContractDefs.DMAnomaly.backStory.Count)];
			return string.Format(story, this.agent.Name, targetAnomaly.Body.displayName.LocalizeBodyName());
		}

		protected override string GetSynopsys()
		{
			if (targetAnomaly == null)
				return "Whoops. Something bad happened here...";

			return string.Format("We would like you to travel to a specific location on {0}. Once there attempt to locate and study the source of the anomalous signal.", targetAnomaly.Body.displayName.LocalizeBodyName());
		}

		protected override string MessageCompleted()
		{
			if (targetAnomaly == null)
				return "Whoops. Something bad happened here...";

			return string.Format("You successfully returned data from the {0} on {1}, well done.", hash, targetAnomaly.Body.displayName.LocalizeBodyName());
		}

		protected override void OnLoad(ConfigNode node)
		{
			r = new System.Random(this.MissionSeed);
			latRand = r.Next(-5, 5);
			lonRand = r.Next(-5, 5);

			body = node.parse("Target_Body", "");

			if (string.IsNullOrEmpty(body))
			{
				DMUtils.Logging("Failed To Load Anomaly Contract Target Body...");
				this.Unregister();
				ContractSystem.Instance.Contracts.Remove(this);
				return;
			}

			hash = node.parse("Target_Anomaly", "");

			if (string.IsNullOrEmpty(hash))
			{
				DMUtils.Logging("Failed To Load Anomaly Contract Target...");
				this.Unregister();
				ContractSystem.Instance.Contracts.Remove(this);
				return;
			}

			targetAnomaly = DMAnomalyList.getAnomalyObject(body, hash);

			if (targetAnomaly == null)
			{
				DMUtils.Logging("Failed To Load Anomaly Contract Object");
				this.Unregister();
				ContractSystem.Instance.Contracts.Remove(this);
				return;	
			}

			lat = targetAnomaly.Lat;
			lon = targetAnomaly.Lon;

			cardNS = NSDirection(lat);
			cardEW = EWDirection(lon);

			if (this.ParameterCount == 0)
			{
				DMUtils.Logging("No Parameters Loaded For This Anomaly Contract; Removing Now...");
				this.Unregister();
				ContractSystem.Instance.Contracts.Remove(this);
				return;
			}
		}

		protected override void OnSave(ConfigNode node)
		{
			node.AddValue("Target_Anomaly", hash);
			node.AddValue("Target_Body", body);
		}

		public override bool MeetRequirements()
		{
			return ProgressTracking.Instance.NodeComplete(new string[] { Planetarium.fetch.Home.bodyName, "Orbit" });
		}

		/// <summary>
		/// Used externally to return the target Celestial Body
		/// </summary>
		/// <param name="cP">Instance of the requested Contract</param>
		/// <returns>Celestial Body object</returns>
		public static CelestialBody TargetBody(Contract c)
		{
			if (c == null || c.GetType() != typeof(DMAnomalyContract))
				return null;

			try
			{
				DMAnomalyContract Instance = (DMAnomalyContract)c;
				if (Instance.targetAnomaly != null)
					return Instance.targetAnomaly.Body;

				return null;
			}
			catch (Exception e)
			{
				Debug.LogError("Error while accessing DMagic Anomaly Contract Target Body\n" + e);
				return null;
			}
		}

		public DMAnomalyObject TargetAnomaly
		{
			get { return targetAnomaly; }
		}

	}
}
