
require 'action_manager'
require 'utility'
require 'default_actions'
require 'status_effects'
require 'AI_basics'
require 'AI_melee'

--the game uses this data internally
standard_data = {
	name = "Typeroach",
	model = "typeroach",
	map_icon = "<color=#859472>t</color>",

	health = 12,

	movement_types = {
		crawl = {
			aggro_distance = 7,
			aggro_speed = 1,
			roam_speed = 0.4,
			roams_in_groups = true,
			unbound = false,
			stomping_immunity = false,
			casting_immunity = false,
			character_radius = 0.4,
			can_use_low_passages = true,
			idle_anim = {clip = "idle#1", speed = 1},
			move_anim = {clip = "move#1", speed = 1},
			stomped_anim = {clip = "stomped#1", speed = 0.5}
		}
	}
}

--custom data
health = standard_data.health
actions = {
	bite = {
		damage_range = {1, 2},
		chance = 85,
		cooldown = 1,
		anim = {clip = "attack#1", speed = 0.6}
	},
	leap = {
		chance = 25,
		cooldown = 3,
		anim = {clip_L = "dodge#1", clip_R = "dodge#2", speed = 1}
	},
	mimic = {
		chance = 25,
		aggro_distance = 1
	}
}
mimic_obj_name = ""
mimicking = false
mimic_anim = 0

function start ()
	mimic_obj_name = render.GetRandomObjectFromCategory("book")
	if math.random(0,100) < actions.mimic.chance then
		mimicking = true
		mimic_anim = 1
		render.SetDissolve(1)
	end
end

--internally called each frame for active crawlers
function update ()

	render.SetDissolve(mimic_anim ^ 4)
	if mimic_anim > 0.01 then
		render.RenderObject(mimic_obj_name, crawler.Position(), movement.Direction(), mimic_anim ^ 2)
	end

	if mimicking then
		mimic_anim = 1
		if targets.PlayerVisible() and vector.Distance(crawler.Position(), player.Position()) < actions.mimic.aggro_distance then
			mimicking = false
			sfx.Play(sfx.Sounds.AGGRO, 0, 1)
			crawler.SetState(crawler.States.COMBAT)
		end
	else
		mimic_anim = math.max(0, mimic_anim - time.FrameDeltaTime())

		if crawler.GetState() == crawler.States.ROAMING then
			update_roaming()
			if #action_queue == 0 and targets.PlayerVisible() and targets.PlayerReachable() then aggro(actions.leap) end
		elseif crawler.GetState() == crawler.States.COMBAT then
			--attack when player goes near mimic
			if mimic_anim > 0 and mimic_anim < 0.75 and #action_queue == 0 and melee_attack_cooldown == 0 and player.AttackTarget() ~= crawler.AsTarget() and vector.Distance(crawler.Position(), player.Position()) < actions.mimic.aggro_distance - 0.1 then
				new_action(Attack:new{parameters = actions.bite, target = player.AsTarget()})
				melee_attack_cooldown = actions.bite.cooldown
			end
			if mimic_anim < 0.75 then
				update_melee_combat(actions.bite, actions.leap)
			end
		end

		drawHealthBar()
	end

	update_actions()
	collision.GetPushed()
end

--internally called every frame if active or every movement simulation tick if abstracted
function abstracted_update (delta_time)
	update_status_effects(delta_time)
	crawler.EnableRoaming(not mimicking)
	crawler.SetVisible(not mimicking)
end

function on_death ()
end
