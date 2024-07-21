
require 'action_manager'
require 'utility'
require 'default_actions'
require 'status_effects'
require 'AI_basics'
require 'AI_melee'

--the game uses this data internally
standard_data = {
	name = "Vertebrae torso",
	model = "vertebrae_torso",
	map_icon = "<color=#a7c9d1>v</color>",

	health = 24,

	movement_types = {
		crawl = {
			aggro_distance = 7,
			aggro_speed = 1,
			roam_speed = 0.4,
			roams_in_groups = false,
			unbound = false,
			stomping_immunity = false,
			casting_immunity = false,
			character_radius = 0.5,
			can_use_low_passages = true,
			idle_anim = {clip = "idle#1", speed = 0.75},
			move_anim = {clip = "move#1", speed = 0.8},
			stomped_anim = {clip = "stomped#1", speed = 1.2}
		}
	}
}

--custom data
health = standard_data.health
actions = {
	bite = {
		damage_range = {4, 5},
		chance = 85,
		cooldown = 1,
		anim = {clip = "attack#1", speed = 1.1}
	}
}

--internally called when crawler is spawned
function start ()
	anim.PlayMisc({clip = "misc#1", speed = 1})
end

--internally called each frame for active crawlers
function update ()

	if anim.Playing(anim.Animations.MISC) then
		return
	end

	if crawler.GetState() == crawler.States.ROAMING then
		update_roaming()
		if #action_queue == 0 and targets.PlayerVisible() and targets.PlayerReachable() then aggro(nil) end
	elseif crawler.GetState() == crawler.States.COMBAT then
		update_melee_combat(actions.bite, nil)
		if #action_queue == 0 then
			anim.LookAt(player.CameraPosition());
		end
	end

	update_actions()
	collision.GetPushed()
	drawHealthBar()
end

--internally called every frame if active, every movement simulation tick if abstracted
function abstracted_update (delta_time)
	update_status_effects(delta_time)
end

function on_death ()
end