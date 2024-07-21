
require 'action_manager'
require 'utility'
require 'default_actions'
require 'status_effects'
require 'AI_basics'
require 'AI_melee'

--the game uses this data internally
standard_data = {
	name = "Pseudocrawler",
	model = "pseudo crawler",
	map_icon = "<color=#b5b5b5>p</color>",

	health = 22,

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
			idle_anim = {clip = "idle#1", speed = 1},
			move_anim = {clip = "move#1", speed = 0.7},
			stomped_anim = {clip = "stomped#1", speed = 1.2}
		},
		carry = {
			aggro_distance = 7,
			aggro_speed = 0.85,
			roam_speed = 0.4,
			roams_in_groups = false,
			unbound = false,
			stomping_immunity = true,
			casting_immunity = false,
			character_radius = 0.8,
			can_use_low_passages = false,
			idle_anim = {clip = "idle#2", speed = 1},
			move_anim = {clip = "move#2", speed = 0.6},
			stomped_anim = {clip = "stomped#2", speed = 1.2, additive_blending = true}
		}
	}
}

--custom data
health = standard_data.health
carrying = false
grab_timer = 0
actions = {
	tendril_slap = {
		damage_range = {3, 4},
		chance = 85,
		cooldown = 1,
		anim = {clip = "attack#1", speed = 1.1}
	},
	hit = {
		damage_range = {3, 5},
		chance = 75,
		cooldown = 1,
		anim = {clip = "attack#2", speed = 1.2}
	},
	leap = {
		chance = 25,
		cooldown = 3,
		anim = {clip_L = "dodge#1", clip_R = "dodge#2", speed = 1.7}
	},
	carry = {
		anim = {clip = "misc#1", speed = 1}
	},
	carry_large = {
		anim = {clip = "misc#2", speed = 1}
	},
	telegraph = {
		duration = 0.25,
		anim = {clip = "misc#3", speed = 1}
	}
}

--carrying another crawler
Carry = Action:new()
function Carry:new (o, parameters, target)
   o = o or Action:new(o, parameters); setmetatable(o, self); self.__index = self
   self.target = target
   self.rotate = true
   return o
end
function Carry:init ()
	if targets.ChildCount(self.target) == 0 then
		anim.PlayMisc(self.parameters.anim)
		movement.SetMovementType(2)
		targets.Call(self.target, "clear_actions")
		parent.AddChild(self.target)
	else
		self:finish()
	end
end
function Carry:update ()
	if self.rotate then
		movement.RotateTowards(targets.Position(self.target))
		parent.LerpChildToBone(0, parent.Bones.TRANSITION, time.PhysicsDeltaTime() * 1.5)
	elseif not carrying then
		parent.LerpChildToBone(0, parent.Bones.TRANSITION, 1)
	end

	if not anim.Playing(anim.Animations.MISC) then
		self:finish()
	end
end
function Carry:anim_event (event_parameters)
	local event = event_parameters[3]
	if event == "ragdoll_rider" then
		targets.Call(self.target, "set_scripted_ragdoll_mode", anim.RagdollModes.APPENDAGES)
		self.rotate = false
	elseif event == "get_mounted" then
		carrying = true
	end
end
function Carry:on_cancel ()
	carrying = false
	movement.SetMovementType(1)
	parent.ClearChildren()
	if self.target ~= nil then
		targets.Call(self.target, "reset_scripted_ragdoll_mode")
	end
end


function set_grab_timer()
	grab_timer = math.random(1, 2)
end
set_grab_timer()

--internally called each frame for active crawlers
function update ()
	if crawler.GetState() == crawler.States.ROAMING then
		update_roaming()
		if #action_queue == 0 and targets.PlayerVisible() and targets.PlayerReachable() then
			if not carrying then
				aggro(actions.leap)
			else
				aggro(nil)
			end
			set_grab_timer()
		end
	elseif crawler.GetState() == crawler.States.COMBAT then
		if not carrying then
			update_melee_combat(actions.tendril_slap, actions.leap)
			--grab another crawler
			if #action_queue == 0 and grab_timer == 0 and not crawler.Is(StatusFX.ENRAGED) and not crawler.Is(StatusFX.CONFUSED) then
				local grab_targets = targets.InRange(movement.CharacterRadius() + 0.1, targets.Types.CRAWLER)
				for i=1,#grab_targets do
					local target = grab_targets[i]
					if targets.ChildCount(target) == 0 and not targets.Call(target, "targeting", crawler.AsTarget()) and not targets.Call(target, "targeted") then
						new_action(Telegraph:new{parameters = actions.telegraph, target = target})
						if targets.Radius(target) < 0.45 then
							new_action(Carry:new{parameters = actions.carry, target = target})
						else
							new_action(Carry:new{parameters = actions.carry_large, target = target})
						end
						break
					end
				end
			end
		else
			update_melee_combat(actions.hit, nil)
		end
	end

	if carrying then
		if parent.ChildCount() > 0 then
			parent.LerpChildToBone(0, parent.Bones.PARENT, 1)
			targets.Call(parent.GetChild(0), "drawHealthBar")
		else
			carrying = false
			movement.SetMovementType(1)
			set_grab_timer()
		end
	else
		drawHealthBar()
	end

	update_actions()
	collision.GetPushed()
end

--internally called every frame if active, every movement simulation tick if abstracted
function abstracted_update (delta_time)
	update_status_effects(delta_time)
	grab_timer = math.max(0, grab_timer - delta_time)
end

function on_death ()
	if parent.ChildCount() > 0 then
		targets.Call(parent.GetChild(0), "reset_scripted_ragdoll_mode")
		parent.ClearChildren()
	end
end
