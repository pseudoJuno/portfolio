
require 'action_manager'
require 'utility'
require 'default_actions'
require 'status_effects'
require 'AI_basics'
require 'AI_melee'

--the game uses this data internally
standard_data = {
	name = "Vertebrae",
	model = "vertebrae_full",
	map_icon = "<color=#a7c9d1>w</color>",

	health = 24,

	movement_types = {
		crawl = {
			aggro_distance = 7,
			aggro_speed = 0.95,
			roam_speed = 0.4,
			roams_in_groups = false,
			unbound = true,
			stomping_immunity = true,
			casting_immunity = false,
			character_radius = 1,
			can_use_low_passages = true,
			idle_anim = {clip = "idle#1", speed = 0.5},
			move_anim = {clip = "move#1", speed = 0.8},
			stomped_anim = {clip = "stomped#1", speed = 1}
		}
	}
}

--custom data
health = standard_data.health
attack_chain = 0
actions = {
	hit = {
		damage_range = {1, 1},
		chance = 100,
		cooldown = 0.15,
		anim = {clip = "attack#1", speed = 1.2}
	},
	knock_over = {
		damage_range = {1, 1},
		knock_over = true,
		chance = 100,
		cooldown = 0.15,
		anim = {clip = "attack#1", speed = 1.2}
	},
	flee = {
		distance = 15
	},
	break_apart = {
		anim = {clip = "misc#1", speed = 1}
	}
}

--action for breaking apart
BreakApart = Action:new()
function BreakApart:new (o, parameters)
   o = o or Action:new(o, parameters); setmetatable(o, self); self.__index = self
   return o
end
function BreakApart:init ()
	anim.PlayMisc(self.parameters.anim)
end
function BreakApart:update ()
	if not anim.Playing(anim.Animations.MISC) then
		self:finish()
	end
end
function BreakApart:anim_event (event_parameters)
	local event = event_parameters[3]
	if event == "spawn_crawler" then
		game.SpawnCrawler("vertebrae_torso")
	elseif event == "die" then
		health = 0
		on_death ()
		crawler.Kill()
		anim.SetRagdollMode(anim.RagdollModes.FULL)
	end
end

--internally called each frame for active crawlers
function update ()

	if crawler.Is(StatusFX.SHACKLED) then
		if #action_queue == 0 then
			new_action(BreakApart:new{parameters = actions.break_apart})
		end
	else
		if crawler.GetState() == crawler.States.ROAMING then
			update_roaming()
			if targets.PlayerVisible() and targets.PlayerReachable() then
				sfx.Play(sfx.Sounds.AGGRO, 0, 1)
				crawler.SetState(crawler.States.COMBAT)
			end
		elseif crawler.GetState() == crawler.States.COMBAT then
			if attack_chain == 0 then
				if update_melee_combat(actions.hit, nil) then
					attack_chain = 1
				end
			else
				if update_melee_combat(actions.knock_over, nil) then
					attack_chain = 0
					new_action(Flee:new{parameters = actions.flee})
				end
			end
			if #action_queue == 0 then
				anim.LookAt(player.CameraPosition());
			end
		end
	end

	update_actions()
	if crawler.GetState() == crawler.States.ROAMING then
		collision.GetPushed()
	end
end

--internally called every frame if active, every movement simulation tick if abstracted
function abstracted_update (delta_time)
	if not crawler.Is(StatusFX.SHACKLED) then
		update_status_effects(delta_time)
	end
end

function on_death ()
end
