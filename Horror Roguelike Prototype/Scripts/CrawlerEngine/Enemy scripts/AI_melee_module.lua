
melee_attack_cooldown = 0
dodge_cooldown = 0
cooldown_end_time = -0.1

function pick_target ()
	if crawler.Is(StatusFX.ENRAGED) or crawler.Is(StatusFX.CONFUSED) then
		return targets.Closest(targets.Types.ANYONE)
	else
		return player.AsTarget()
	end
end

function update_melee_combat(attack, dodge)

	--attacking
	local startedAnAttack = false
	local attackedByPlayer = (player.AttackTarget() == crawler.AsTarget())
	local queuedAttackAllowed = (#action_queue == 1 and action_queue[1].allow_queued_attack)
	if #action_queue == 0 or action_queue[1].allow_AI_movement or queuedAttackAllowed then
		
		local cooldown_end = 0
		if movement.DestinationInAttackRange() then
			--count down the rest of the cooldown only when in attack range; this gives the player the attack initiative
			cooldown_end = cooldown_end_time
		end
		melee_attack_cooldown = math.max(cooldown_end, melee_attack_cooldown - time.FrameDeltaTime() * time.StatusTimeScale() - time.StatusTimeIncrements())
		--find a target
		local target = pick_target()
		local targetIsPlayer = (target == player.AsTarget())

		if target ~= nil then

			--quit combat if the player can't be reached
			if targetIsPlayer and not targets.PlayerReachable() then
				crawler.SetState(crawler.States.ROAMING)
				movement.SetDestination(crawler.Position(), 1)
				return
			end

			if not targetIsPlayer or targets.PlayerVisible() or vector.Distance(crawler.Position(), player.Position()) < 5 then
				--set target position as destination
				movement.SetDestination(targets.Position(target), targets.Radius(target) + movement.CharacterRadius())
				--attack
				if melee_attack_cooldown <= 0 and (#action_queue == 0 or queuedAttackAllowed) then
					--start the specified attack when in range or when counterattacking player
					if movement.DestinationInAttackRange() and ((not attackedByPlayer and melee_attack_cooldown == cooldown_end_time) or (targetIsPlayer and player.Attacked())) then
						new_action(Attack:new{parameters = attack, target = target})
						melee_attack_cooldown = attack.cooldown
						startedAnAttack = true
					end
				end
			else
				--quit combat if we lose the player
				if movement.DestinationReached() then crawler.SetState(crawler.States.ROAMING) end
			end
		end
	end

	--dodging
	if dodge ~= nil and player.AttackDodgeCheck() and attackedByPlayer then
		try_dodge(dodge)
	end

	return startedAnAttack
end

function try_dodge(dodge)
	if dodge_cooldown == 0 and not crawler.Is(StatusFX.SHACKLED) and not crawler.Is(StatusFX.SLOWED) then
		if math.random(0, 100) <= dodge.chance then
			clear_actions()
			new_action(Leap:new{parameters = dodge, anim_side = math.random(0, 1) == 1, direction = nil, distance = 2, speed = 1})
			dodge_cooldown = dodge.cooldown
			player.SetAttackDodged()
		end
	end
	dodge_cooldown = math.max(0, dodge_cooldown - 1);
end