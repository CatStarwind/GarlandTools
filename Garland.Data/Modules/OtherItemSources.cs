﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Garland.Data.Modules
{
    public class OtherItemSources : Module
    {
        Dictionary<string, dynamic> _venturesByName;

        public override string Name => "Other Item Sources";

        public override void Start()
        {
            _venturesByName = _builder.Db.Ventures.Where(v => v.name != null).ToDictionary(v => (string)v.name);

            var lines = Utils.Tsv("Supplemental\\FFXIV Data - Items.tsv");
            foreach (var line in lines.Skip(1))
            {
                var type = line[1];
                var args = line.Skip(2).Where(c => c != "").ToArray();
                var itemName = line[0];
                var item = _builder.Db.ItemsByName[itemName];

                switch (type)
                {
                    case "Desynth":
                        BuildDesynth(item, args);
                        break;

                    case "Reduce":
                        BuildReduce(item, args);
                        break;

                    case "Loot":
                        BuildLoot(item, args);
                        break;

                    case "Venture":
                        BuildVentures(item, args);
                        break;

                    case "Node":
                        BuildNodes(item, args);
                        break;

                    case "FishingSpot":
                        BuildFishingSpots(item, args);
                        break;

                    case "Instance":
                        BuildInstances(item, args);
                        break;

                    case "Voyage":
                        BuildVoyages(item, args);
                        break;

                    case "Gardening":
                        BuildGardening(item, args);
                        break;

                    default:
                        throw new NotImplementedException();
                }
            }
        }

        void BuildGardening(dynamic item, string[] sources)
        {
            foreach (string seedItemName in sources)
            {
                var seedItem = _builder.Db.ItemsByName[seedItemName];
                Items.AddGardeningPlant(_builder, seedItem, item);
            }
        }

        void BuildDesynth(dynamic item, string[] sources)
        {
            if (item.desynthedFrom != null)
                throw new InvalidOperationException("item.desynthedFrom already exists.");

            item.desynthedFrom = new JArray();
            foreach (string itemName in sources)
            {
                var desynthItem = _builder.Db.ItemsByName[itemName];
                item.desynthedFrom.Add((int)desynthItem.id);
                _builder.Db.AddReference(item, "item", (int)desynthItem.id, false);
                _builder.Db.AddReference(desynthItem, "item", (int)item.id, false);
            }
        }

        void BuildReduce(dynamic item, string[] sources)
        {
            if (item.reducedFrom != null)
                throw new InvalidOperationException("item.reducedFrom already exists.");

            item.reducedFrom = new JArray();
            foreach (string sourceItemName in sources)
            {
                var sourceItem = _builder.Db.ItemsByName[sourceItemName];
                if (sourceItem.reducesTo == null)
                    sourceItem.reducesTo = new JArray();
                sourceItem.reducesTo.Add((int)item.id);
                item.reducedFrom.Add((int)sourceItem.id);

                _builder.Db.AddReference(sourceItem, "item", (int)item.id, false);
                _builder.Db.AddReference(item, "item", (int)sourceItem.id, true);

                // Set aetherial reduction info on the gathering node view.
                foreach (var view in _builder.Db.NodeViews)
                {
                    foreach (var itemView in view.items)
                    {
                        if (itemView.id == sourceItem.id && itemView.reduce == null)
                        {
                            itemView.reduce = new JObject();
                            itemView.reduce.item = item.en.name;
                            itemView.reduce.icon = item.icon;
                        }
                    }
                }
            }
        }

        void BuildLoot(dynamic item, string[] sources)
        {
            if (item.treasure != null)
                throw new InvalidOperationException("item.treasure already exists.");

            var generators = sources.Select(j => _builder.Db.ItemsByName[j]).ToArray();
            item.treasure = new JArray(generators.Select(i => (int)i.id));

            foreach (var generator in generators)
            {
                if (generator.loot == null)
                    generator.loot = new JArray();
                generator.loot.Add((int)item.id);
                _builder.Db.AddReference(generator, "item", (int)item.id, false);
                _builder.Db.AddReference(item, "item", (int)generator.id, true);
            }
        }

        void BuildVentures(dynamic item, string[] sources)
        {
            if (item.ventures != null)
                throw new InvalidOperationException("item.ventures already exists.");
            var ventureIds = sources.Select(j => (int)_venturesByName[j].id);
            item.ventures = new JArray(ventureIds);
        }

        void BuildVoyages(dynamic item, string[] sources)
        {
            if (item.voyages != null)
                throw new InvalidOperationException("item.voyages already exists.");
            item.voyages = new JArray(sources);
        }

        void BuildNodes(dynamic item, string[] sources)
        {
            if (item.nodes == null)
                item.nodes = new JArray();

            foreach (var id in sources.Select(int.Parse))
            {
                item.nodes.Add(id);

                int itemId = item.id;

                var node = _builder.Db.Nodes.First(n => n.id == id);
                dynamic w = new JObject();
                w.id = itemId;
                w.slot = "?"; // Only hidden items here
                node.items.Add(w);

                _builder.Db.AddReference(node, "item", itemId, false);
                _builder.Db.AddReference(item, "node", id, true);
            }
        }

        void BuildFishingSpots(dynamic item, string[] sources)
        {
            if (item.fishingSpots == null)
                item.fishingSpots = new JArray();

            foreach (var name in sources)
            {
                dynamic w = new JObject();
                w.id = (int)item.id;
                w.lvl = (int)item.ilvl;

                var spot = _builder.Db.FishingSpots.First(f => f.name == name);
                item.fishingSpots.Add((int)spot.id);
                spot.items.Add(w);

                _builder.Db.AddReference(spot, "item", (int)item.id, false);
            }
        }

        void BuildInstances(dynamic item, string[] sources)
        {
            if (item.instances == null)
                item.instances = new JArray();

            int itemId = item.id;

            foreach (var name in sources)
            {
                var instance = _builder.Db.Instances.First(i => i.en.name == name);
                int instanceId = instance.id;
                if (instance.rewards == null)
                    instance.rewards = new JArray();
                instance.rewards.Add(itemId);
                item.instances.Add(instanceId);

                _builder.Db.AddReference(instance, "item", itemId, false);
                _builder.Db.AddReference(item, "instance", instanceId, true);
            }
        }
    }
}