using System;
using Menu.Remix.MixedUI;
using UnityEngine;

namespace SlopeWorld;

public class SlopeWorldOptions : OptionInterface
{	
	public readonly Configurable<bool> EnablePatches;
	public readonly Configurable<bool> SillyMode;

	public SlopeWorldOptions()
	{
		EnablePatches = config.Bind(nameof(EnablePatches), true);
		SillyMode = config.Bind(nameof(SillyMode), false);
	}

	public override void Initialize()
	{
		try {

            Tabs = [ new OpTab(this, "Config") ];

            string description = "Slope World Settings";
			Vector2 pos = new Vector2(32, 530);

            Tabs[0].AddItems(
                new OpLabel(pos, new Vector2(300, 64), "Settings", FLabelAlignment.Left, true)
            );
             
            description = "Enable my patches for slow crawl turns, and bouncy spears/eslide bouncing\nDisable if you are curious what the raw dune physics on slopes is like";
            pos.y -= 36;
			
            Tabs[0].AddItems(
				new OpLabel(pos + new Vector2(32, -3), new Vector2(164, 32), "Enable Patches", FLabelAlignment.Left, false)
				{ description = description },

				new OpCheckBox(EnablePatches, pos)
				{ description = description }
			);

            description = "A silly bug that happened while I was making the crawl turn patch :P";
			pos.y -= 30;

            Tabs[0].AddItems(
				new OpLabel(pos + new Vector2(32, -3), new Vector2(164, 32), "Silly Mode", FLabelAlignment.Left, false)
				{ description = description },

				new OpCheckBox(SillyMode, pos)
				{ description = description }
			);

		}

		catch(Exception ex)
		{
			SlopeWorld.LogError(ex);
		}
	}

    public override string ValidationString()
    {
        return $"{(EnablePatches.Value ? "Patches-Enabled" : "Patches-Disabled")} {(SillyMode.Value ? "Silly!!" : "No-Silly :(")} {base.ValidationString()}";
    }
}
