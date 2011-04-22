﻿using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Transactions;
using System.Web;
using System.Web.Mvc;
using PlasmaShared;
using ZkData;

namespace ZeroKWeb.Controllers
{
	public class PlanetwarsController: Controller
	{
		//
		// GET: /Planetwars/

		[Auth]
		public ActionResult ChangePlayerRights(int clanID, int accountID)
		{
			var db = new ZkDataContext();
			var clan = db.Clans.Single(c => clanID == c.ClanID);
			if (!(Global.Account.HasClanRights && clan.ClanID == Global.Account.ClanID || Global.Account.IsZeroKAdmin)) return Content("Unauthorized");
			var kickee = db.Accounts.Single(a => a.AccountID == accountID);
			if (kickee.IsClanFounder) return Content("Clan founders can't be modified.");
			kickee.HasClanRights = !kickee.HasClanRights;
			var ev = Global.CreateEvent("{0} {1} {2} rights to clan {3}", Global.Account, kickee.HasClanRights ? "gave" : "took", kickee, clan);
			db.Events.InsertOnSubmit(ev);
			db.SubmitChanges();
			return RedirectToAction("Clan", new { id = clanID });
		}

	
		/// <summary>
		/// Shows clan page
		/// </summary>
		/// <returns></returns>
		public ActionResult Clan(int id)
		{
			var db = new ZkDataContext();
			var clan = db.Clans.First(x => x.ClanID == id);
			if (Global.ClanID == clan.ClanID)
			{
				if (clan.ForumThread != null)
				{
					clan.ForumThread.UpdateLastRead(Global.AccountID, false);
					db.SubmitChanges();
				}
			}
			return View(clan);
		}

		public ActionResult ClanList()
		{
			var db = new ZkDataContext();

			return View(db.Clans.AsQueryable());
		}

		[Auth]
		public ActionResult CreateClan()
		{
			if (Global.Account.Clan == null || (Global.Account.HasClanRights)) return View(Global.Clan ?? new Clan());
			else return Content("You already have clan and you dont have rights to it");
		}

		public Bitmap GenerateGalaxyImage(int galaxyID, double zoom = 1, double antiAliasingFactor = 4)
		{
			zoom *= antiAliasingFactor;
			using (var db = new ZkDataContext())
			{
				var gal = db.Galaxies.Single(x => x.GalaxyID == galaxyID);

				using (var background = Image.FromFile(Server.MapPath("/img/galaxies/" + gal.ImageName)))
				{
					var im = new Bitmap((int)(background.Width*zoom), (int)(background.Height*zoom));
					using (var gr = Graphics.FromImage(im))
					{
						gr.DrawImage(background, 0, 0, im.Width, im.Height);

						using (var pen = new Pen(Color.FromArgb(255, 180, 180, 180), (int)(1*zoom)))
						{
							foreach (var l in gal.Links)
							{
								gr.DrawLine(pen,
								            (int)(l.PlanetByPlanetID1.X*im.Width),
								            (int)(l.PlanetByPlanetID1.Y*im.Height),
								            (int)(l.PlanetByPlanetID2.X*im.Width),
								            (int)(l.PlanetByPlanetID2.Y*im.Height));
							}
						}

						foreach (var p in gal.Planets)
						{
							using (var pi = Image.FromFile(Server.MapPath("/img/planets/" + p.Resource.MapPlanetWarsIcon)))
							{
								var aspect = pi.Height/(double)pi.Width;
								var width = (int)(p.Resource.PlanetWarsIconSize*zoom);
								var height = (int)(width*aspect);
								gr.DrawImage(pi, (int)(p.X*im.Width) - width/2, (int)(p.Y*im.Height) - height/2, width, height);
							}
						}
						if (antiAliasingFactor == 1) return im;
						else
						{
							zoom /= antiAliasingFactor;
							return im.GetResized((int)(background.Width*zoom), (int)(background.Height*zoom), InterpolationMode.HighQualityBicubic);
						}
					}
				}
			}
		}

		public ActionResult Index(int? galaxyID = null)
		{
			var db = new ZkDataContext();

			Galaxy gal;
			if (galaxyID != null) gal = db.Galaxies.Single(x => x.GalaxyID == galaxyID);
			else gal = db.Galaxies.Single(x => x.IsDefault);

			var cachePath = Server.MapPath(string.Format("/img/galaxies/render_{0}.jpg", gal.GalaxyID));
			if (gal.IsDirty || !System.IO.File.Exists(cachePath))
			{
				using (var im = GenerateGalaxyImage(gal.GalaxyID))
				{
					im.Save(cachePath);
					gal.IsDirty = false;
					gal.Width = im.Width;
					gal.Height = im.Height;
					db.SubmitChanges();
				}
			}
			return View("Galaxy", gal);
		}

		[Auth]
		public ActionResult JoinClan(int id, string password)
		{
			var db = new ZkDataContext();
			var clan = db.Clans.Single(x => x.ClanID == id);
			if (clan.CanJoin(Global.Account))
			{
				if (!string.IsNullOrEmpty(clan.Password) && clan.Password != password) return View(clan.ClanID);
				else
				{
					var acc = db.Accounts.Single(x => x.AccountID == Global.AccountID);
					acc.ClanID = clan.ClanID;
					db.SubmitChanges();
					return RedirectToAction("Clan", new { id = clan.ClanID });
				}
			}
			else return Content("You cannot join this clan");
		}

		[Auth]
		public ActionResult KickPlayerFromClan(int clanID, int accountID)
		{
			var db = new ZkDataContext();
			var clan = db.Clans.Single(c => clanID == c.ClanID);
			// todo: disallow kicking after the round starts
			if (!(Global.Account.HasClanRights && clan.ClanID == Global.Account.ClanID)) return Content("Unauthorized");
			var kickee = db.Accounts.Single(a => a.AccountID == accountID);
			if (kickee.IsClanFounder) return Content("Clan founders can't be kicked.");
			kickee.ClanID = null;
			db.SubmitChanges();
			return RedirectToAction("Clan", new { id = clanID });
		}

		public ActionResult Planet(int id)
		{
			var db = new ZkDataContext();
			var planet = db.Planets.Single(x => x.PlanetID == id);
			if (planet.ForumThread != null)
			{
				planet.ForumThread.UpdateLastRead(Global.AccountID, false);
				db.SubmitChanges();
			}
			return View(planet);
		}

		[Auth]
		public ActionResult SendDropships(int planetID, int count)
		{
			var db = new ZkDataContext();
			using (var scope = new TransactionScope())
			{
				var acc = db.Accounts.Single(x => x.AccountID == Global.AccountID);
				var cnt = Math.Max(count, 0);
				cnt = Math.Min(cnt, acc.DropshipCount ?? 0);
				acc.DropshipCount = (acc.DropshipCount ?? 0) - cnt;
				var pac = acc.AccountPlanets.SingleOrDefault(x => x.PlanetID == planetID);
				if (pac == null)
				{
					pac = new AccountPlanet() { AccountID = Global.AccountID, PlanetID = planetID };
					db.AccountPlanets.InsertOnSubmit(pac);
				}
				pac.DropshipCount += cnt;
				if (cnt > 0) db.Events.InsertOnSubmit(Global.CreateEvent("{0} sends {1} dropships to {2}", acc, cnt, pac.Planet));
				db.SubmitChanges();
				scope.Complete();
			}
			return RedirectToAction("Planet", new { id = planetID });
		}

		[Auth]
		public ActionResult SubmitBuyStructure(int planetID, int structureTypeID)
		{
			var db = new ZkDataContext();
			var planet = db.Planets.Single(p => p.PlanetID == planetID);
			if (Global.Account.AccountID != planet.OwnerAccountID) return Content("Planet is not under control.");
			var structureType = db.StructureTypes.SingleOrDefault(s => s.StructureTypeID == structureTypeID);
			if (structureType == null) return Content("Structure type does not exist.");
			if (!structureType.IsBuildable) return Content("Structure is not buildable.");

			// assumes you can only build level 1 structures! if higher level structures can be built directly, we should check down the upgrade chain too
			if (HasStructureOrUpgrades(db, planet, structureType)) return Content("Structure or its upgrades already built");

			if (Global.Account.Credits < structureType.Cost) return Content("Insufficient credits.");
			Global.Account.Credits -= structureType.Cost;

			var newBuilding = new PlanetStructure { StructureTypeID = structureTypeID, PlanetID = planetID };
			db.PlanetStructures.InsertOnSubmit(newBuilding);
			db.SubmitChanges();

			Global.CreateEvent("{0} has built a {1} on {2}.", Global.Account, newBuilding, planet);
			return RedirectToAction("Planet", new { id = planet.PlanetID });
		}


		[Auth]
		public ActionResult SubmitCreateClan(Clan clan, HttpPostedFileBase image)
		{
			var db = new ZkDataContext();
			var created = clan.ClanID == 0; // existing clan vs creation
			if (!created)
			{
				if (!Global.Account.HasClanRights || clan.ClanID != Global.Account.ClanID) return Content("Unauthorized");
				var orgClan = db.Clans.Single(x => x.ClanID == clan.ClanID);
				orgClan.ClanName = clan.ClanName;
				orgClan.LeaderTitle = clan.LeaderTitle;
				orgClan.Shortcut = clan.Shortcut;
				orgClan.Description = clan.Description;
				orgClan.SecretTopic = clan.SecretTopic;
				orgClan.Password = clan.Password;
				//orgClan.DbCopyProperties(clan); 
			}
			else
			{
				if (Global.Clan != null) return Content("You already have a clan");
				db.Clans.InsertOnSubmit(clan);
			}
			if (string.IsNullOrEmpty(clan.ClanName) || string.IsNullOrEmpty(clan.Shortcut)) return Content("Name and shortcut cannot be empty!");

			if (created && (image == null || image.ContentLength == 0)) return Content("Upload image");
			if (image != null && image.ContentLength > 0)
			{
				var im = Image.FromStream(image.InputStream);
				if (im.Width != 64 || im.Height != 64) im = im.GetResized(64, 64, InterpolationMode.HighQualityBicubic);
				db.SubmitChanges(); // needed to get clan id for image url - stupid way really
				im.Save(Server.MapPath(clan.GetImageUrl()));
			}
			db.SubmitChanges();

			if (created) // we created a new clan, set self as founder and rights
			{
				var acc = db.Accounts.Single(x => x.AccountID == Global.AccountID);
				acc.ClanID = clan.ClanID;
				acc.IsClanFounder = true;
				acc.HasClanRights = true;
				db.SubmitChanges();
			}

			return RedirectToAction("Clan", new { id = clan.ClanID });
		}

		[Auth]
		public ActionResult SubmitRenamePlanet(int planetID, string newName)
		{
			if (String.IsNullOrWhiteSpace(newName)) return Content("Error: the planet must have a name.");
			var db = new ZkDataContext();
			var planet = db.Planets.Single(p => p.PlanetID == planetID);
			if (Global.Account.AccountID != planet.OwnerAccountID) return Content("Unauthorized");
			db.Events.InsertOnSubmit(Global.CreateEvent("{0} renamed planet {1} form {2} to {3}", Global.Account, planet, planet.Name, newName));
			planet.Name = newName;
			db.SubmitChanges();
			return RedirectToAction("Planet", new { id = planet.PlanetID });
		}


		[Auth]
		public ActionResult SubmitUpgradeStructure(int planetID, int structureTypeID)
		{
			var db = new ZkDataContext();
			var planet = db.Planets.Single(p => p.PlanetID == planetID);
			if (Global.Account.AccountID != planet.OwnerAccountID) return Content("Planet is not under control.");
			var oldStructure = db.PlanetStructures.SingleOrDefault(s => s.PlanetID == planetID && s.StructureTypeID == structureTypeID);
			if (oldStructure == null) return Content("Structure does not exist");
			if (oldStructure.StructureType.UpgradesToStructureID == null) return Content("Structure can't be upgraded.");
			if (oldStructure.IsDestroyed) return Content("Can't upgrade a destroyed structure");

			var newStructureType = db.StructureTypes.Single(s => s.StructureTypeID == oldStructure.StructureType.UpgradesToStructureID);
			if (Global.Account.Credits < newStructureType.Cost) return Content("Insufficient credits.");
			Global.Account.Credits -= newStructureType.Cost;

			var newStructure = new PlanetStructure { PlanetID = planetID, StructureTypeID = oldStructure.StructureTypeID };

			db.PlanetStructures.InsertOnSubmit(newStructure);
			db.PlanetStructures.DeleteOnSubmit(oldStructure);

			Global.CreateEvent("{0} has built a {1} on {2}.", Global.Account, newStructure, planet);
			return RedirectToAction("Planet", new { id = planet.PlanetID });
		}

		bool HasStructureOrUpgrades(ZkDataContext db, Planet planet, StructureType structureType)
		{
			// has found stucture in tech tree
			if (planet.PlanetStructures.Any(s => structureType.UpgradesToStructureID == s.StructureTypeID)) return true;
			// has reached the end of the tech tree, no structure found
			if (structureType.UpgradesToStructureID == null) return false;
			// search the next step in the tech tree
			return HasStructureOrUpgrades(db, planet, db.StructureTypes.Single(s => s.StructureTypeID == structureType.UpgradesToStructureID));
		}
	}
}