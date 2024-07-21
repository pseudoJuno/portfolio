
--base class for all actions
Action = { parameters = {}, target = nil, initialized = false, functionally_completed = false, finished = false, allow_movement = false, allow_queued_attack = false, allow_AI_movement = false }
function Action:new (o, parameters)
	o = o or {}; setmetatable(o, self); self.__index = self
	self.parameters = parameters
	self.target = nil
	self.initialized = false
	self.functionally_completed = false
	self.finished = false
	self.allow_movement = false
	self.allow_queued_attack = false
	self.allow_AI_movement = false
	return o
end
--starting the action
function Action:init ()
end
--updating it
function Action:update ()
end
--getting animation events
function Action:anim_event (event_parameters)
end
--handling canceling (if the target is deleted for example)
function Action:on_cancel ()
end
--finishing the action
function Action:finish ()
	self.finished = true
end
--canceling the action (manually from action update or init)
function Action:cancel (mode)
	anim.Stop()
	self:on_cancel()
	self.finished = true
end
--discard the target reference so that our action is not canceled if its target dies
--(eg. if the action is an attack and we've done damage that might kill our target)
function Action:set_completed_and_discard_target ()
	self.functionally_completed = true
	self.target = nil
end


--helper functions
function hit_roll(hit_chance)
	return math.random(0, 100) <= hit_chance
end
function rnd_dmg(damage_range)
	return math.random(damage_range[1], damage_range[2])
end
function get_attack_force(target)
	return vector.Multiply(vector.Direction(crawler.Position(), targets.Position(target)), 1.6)
end

--attack
Attack = Action:new()
function Attack:new (o, parameters, target)
   o = o or Action:new(o, parameters); setmetatable(o, self); self.__index = self
   self.target = target
   self.allow_AI_movement = true
   return o
end
function Attack:init ()
	anim.PlayAttack(self.parameters.anim)
end
function Attack:update ()
	if self.target ~= nil then
		movement.RotateTowards(targets.Position(self.target))
	end

	if not anim.Playing(anim.Animations.ATTACK) then
		self:finish()
	end
end
function Attack:anim_event (event_parameters)
	if hit_roll(self.parameters.chance) then
		local dmg = rnd_dmg(self.parameters.damage_range);

		if targets.Type(self.target) == targets.Types.PLAYER then
			if self.parameters.knock_over ~= nil and self.parameters.knock_over then
				player.PlayPOVAnim(player.POVAnims.FALL_OVER)
			elseif movement.Unbound() then
				player.PlayPOVAnim(player.POVAnims.HARD_HIT)
			else
				player.PlayPOVAnim(player.POVAnims.HIT)
			end
			player.ChangeStat(player.Stats.HP, -dmg)
			render.CombatText("-" .. dmg, vector.Add(crawler.Position(), vector.Multiply(movement.Direction(), movement.CharacterRadius() + 0.1)), 1)
		else
			targets.Call(self.target, "take_damage", dmg, true, false, get_attack_force(self.target))
		end
	else
		render.CombatText("miss", vector.Add(crawler.Position(), vector.Multiply(movement.Direction(), movement.CharacterRadius() + 0.1)), 1)
		player.PlayPOVAnim(player.POVAnims.MISS)
	end
	self:set_completed_and_discard_target()
	self.allow_AI_movement = false
end

--projectile attack
ProjectileAttack = Action:new()
function ProjectileAttack:new (o, parameters, target)
   o = o or Action:new(o, parameters); setmetatable(o, self); self.__index = self
   self.target = target
   self.timer = 0
   return o
end
function ProjectileAttack:init ()
	anim.PlayAttack(self.parameters.anim)
end
function ProjectileAttack:update ()
	if self.target ~= nil then
		movement.RotateTowards(targets.Position(self.target))
	end

	self.timer = self.timer + time.FrameDeltaTime() * time.StatusTimeScale() + time.StatusTimeIncrements()
	if not anim.Playing(anim.Animations.ATTACK) and self.timer >= self.parameters.duration then
		self:finish()
	end
end
function ProjectileAttack:anim_event (event_parameters)
	for i = 1, self.parameters.projectile_count do
		local angle = vector.VectorToAngle(vector.Direction(crawler.Position(), targets.Position(self.target))) + ((i-1) / math.max(1, self.parameters.projectile_count-1) - 0.5) * self.parameters.spread
		game.SpawnProjectile(self.parameters.projectile, vector.AngleToVector(angle), self.parameters.speed[1], self.parameters.speed[2], self.parameters.dmg)
	end
	self:set_completed_and_discard_target()
end

--aggro
Aggro = Action:new()
function Aggro:new (o, parameters)
   o = o or Action:new(o, parameters); setmetatable(o, self); self.__index = self
   self.timer = 0
   return o
end
function Aggro:init ()
	sfx.Play(sfx.Sounds.AGGRO, 0, 1)
	crawler.SetState(crawler.States.COMBAT)
	movement.SetDestination(player.Position(), 0)
	self.timer = 0.5
end
function Aggro:update ()
	movement.RotateTowards(player.Position())
	self.timer = math.max(0, self.timer - time.FrameDeltaTime())

	if self.timer == 0 then
		self:finish()
	end
end

--leap
Leap = Action:new()
function Leap:new (o, parameters, anim_side, direction, distance, speed)
   o = o or Action:new(o, parameters); setmetatable(o, self); self.__index = self
   self.allow_queued_attack = true
   self.anim_side = anim_side
   self.direction = direction
   self.distance = distance
   self.speed = speed
   return o
end
function Leap:init ()
	if self.direction ~= nil then
		self.anim_side = movement.DirectionOnLeftSide(self.direction)
	end
	anim.PlayDodge(self.parameters.anim, self.anim_side, self.speed)
end
function Leap:update ()
	local progress = anim.NormalizedTime(anim.Animations.DODGE)
	local multi = ((1 - math.abs((0.5 - progress) * 2)) * 0.75 + 0.5) ^ 2;
	local dir = nil
	if self.direction == nil then
		dir = vector.Perpendicular(vector.Direction(crawler.Position(), player.Position()))
		if self.anim_side then dir = vector.Multiply(dir, -1) end
	else
		dir = self.direction
	end
    movement.Move(vector.Multiply(dir, self.distance * self.speed * multi));
	movement.RotateTowards(player.Position())

	if not anim.Playing(anim.Animations.DODGE) then
		self:finish()
	end
end

--telegraph
Telegraph = Action:new()
function Telegraph:new (o, parameters, target)
   o = o or Action:new(o, parameters); setmetatable(o, self); self.__index = self
   self.target = target
   self.timer = 0
   return o
end
function Telegraph:init ()
	anim.PlayMisc(self.parameters.anim)
	self.timer = self.parameters.duration
end
function Telegraph:update ()
	if self.target ~= nil then
		movement.RotateTowards(targets.Position(self.target))
		if targets.Type(self.target) == targets.Types.CRAWLER then
			targets.Func(self.target, "movement").Stop()
			targets.Func(self.target, "movement").RotateTowards(crawler.Position())
		end
	end
	if not anim.Playing(anim.Animations.MISC) then
		if self.target ~= nil then
			self:set_completed_and_discard_target()
		end
		self.timer = math.max(0, self.timer - time.FrameDeltaTime() * time.StatusTimeScale() - time.StatusTimeIncrements())
	end

	if self.timer == 0 then
		self:finish()
	end
end

--flee
Flee = Action:new()
function Flee:new (o, parameters)
   o = o or Action:new(o, parameters); setmetatable(o, self); self.__index = self
   self.allow_movement = true
   return o
end
function Flee:init ()
	movement.SetDestination(movement.FindPositionAtDistance(crawler.Position(), self.parameters.distance), 1)
end
function Flee:update ()
	if crawler.Is(StatusFX.SHACKLED) then
		self:finish()
	elseif movement.DestinationReached() then
		crawler.SetState(crawler.States.ROAMING)
		self:finish()
	end
end
