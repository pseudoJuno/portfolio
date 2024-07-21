
require 'action_manager'
require 'utility'
require 'default_actions'
require 'status_effects'
require 'AI_basics'
require 'AI_melee'
require 'AI_ranged'

--the game uses this data internally
standard_data = {
	name = "Gallus",
	model = "gallus",
	map_icon = "<color=#ffffff>g</color>",

	health = 12,

	movement_types = {
		crawl = {
			aggro_distance = 16,
			aggro_speed = 0.8,
			roam_speed = 0.6,
			roams_in_groups = true,
			unbound = false,
			stomping_immunity = false,
			casting_immunity = false,
			character_radius = 0.35,
			can_use_low_passages = true,
			idle_anim = {clip = "idle#1", speed = 1},
			move_anim = {clip = "move#1", speed = 1},
			stomped_anim = {clip = "stomped#1", speed = 1.4}
		}
	}
}

--custom data
health = standard_data.health
actions = {
	cast = {
		range = 9,
		min_range = 3,
		projectile = "astral skull",
		projectile_count = 1,
		dmg = 4,
		speed = {0.5, 1},
		spread = 0,
		duration = 0.75,
		cooldown_range = {0.75, 1.5},
		anim = {clip = "attack#1", speed = 1.2}
	},
	double_cast = {
		range = 9,
		min_range = 3,
		projectile = "astral skull",
		projectile_count = 2,
		dmg = 4,
		speed = {0.5, 1},
		spread = 45,
		duration = 1,
		cooldown_range = {0.75, 1.5},
		anim = {clip = "attack#1", speed = 1.2}
	},
	triple_cast = {
		range = 9,
		min_range = 3,
		projectile = "astral skull",
		projectile_count = 3,
		dmg = 4,
		speed = {0.5, 1},
		spread = 55,
		duration = 1.25,
		cooldown_range = {1, 1.5},
		anim = {clip = "attack#1", speed = 1.2}
	},
	bite = {
		damage_range = {1, 2},
		chance = 85,
		cooldown = 1,
		anim = {clip = "attack#2", speed = 1.5}
	}
}
selected_action = actions.cast

function select_ranged_attack()
	local rng = math.random(3)
	if rng == 1 then
		selected_action = actions.cast
	elseif rng == 2 then
		selected_action = actions.double_cast
	else
		selected_action = actions.triple_cast
	end
end

--internally called each frame for active crawlers
function update ()

	if crawler.GetState() == crawler.States.ROAMING then
		update_roaming()
		if #action_queue == 0 and targets.PlayerVisible() then
			crawler.SetState(crawler.States.COMBAT)
			setAttackCooldown({0.1, 0.2})
		end
	elseif crawler.GetState() == crawler.States.COMBAT then
		if selected_action == actions.bite then
			update_melee_combat(selected_action)
			if vector.Distance(crawler.Position(), player.Position()) > 2.5 then
				select_ranged_attack()
			end
			anim.LookAt(player.CameraPosition());
		else
			if update_ranged_combat(selected_action) then
				select_ranged_attack()
			end
			if vector.Distance(crawler.Position(), player.Position()) < 2 then
				selected_action = actions.bite
			end
			if not anim.Playing(anim.Animations.ATTACK) then
				anim.LookAt(player.CameraPosition());
			end
		end
	end

	update_actions()
	collision.GetPushed()
	drawHealthBar()
end

--internally called every frame if active or every movement simulation tick if abstracted
function abstracted_update (delta_time)
	update_status_effects(delta_time)
end

function on_death ()
end
