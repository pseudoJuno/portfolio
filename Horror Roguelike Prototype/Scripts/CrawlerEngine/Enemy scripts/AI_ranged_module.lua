
ranged_attack_cooldown = 0.5
flee_pos = nil
backup_pos = nil
back_pos_update_timer = 0

function pick_target ()
	return player.AsTarget()
end

function setAttackCooldown (range)
	ranged_attack_cooldown = range[1] + math.random() * (range[2] - range[1])
end

function update_ranged_combat(attack)
	local startedAnAttack = false

	if #action_queue == 0 then

		ranged_attack_cooldown = math.max(0, ranged_attack_cooldown - time.FrameDeltaTime() * time.StatusTimeScale() - time.StatusTimeIncrements())

		if player.AttackTarget() == crawler.AsTarget() then
			movement.SetDestination(player.Position(), player.Radius() + movement.CharacterRadius())
		else
			local crawler_pos = crawler.Position()
			local target = pick_target()
			local target_pos = targets.Position(target)
			local destination_pos = movement.GetDestination();

			local target_dist = vector.Distance(crawler_pos, target_pos)
			local destination_to_target_dist = vector.Distance(destination_pos, target_pos)

			local flee_distance = attack.range / 2

			local blockedLineOfSight = collision.ThickLinecastObstacles(crawler_pos, target_pos, 0.75)

			--face the player when standing still
			if time.MovementTimeScale() < 0.01 then
				movement.RotateTowards(targets.Position(target))
			end

			--sweep player's line of sight for viable position
			if back_pos_update_timer == 0 then
				if backup_pos == nil or destination_to_target_dist < attack.range - 1 or destination_to_target_dist > attack.range + 1 or blockedLineOfSight then
					
					local furthest_pos = sweepPlayerLineOfSight(crawler_pos, target_pos, flee_distance, attack.range)

					if backup_pos == nil or furthest_pos == nil then
						backup_pos = furthest_pos
					elseif furthest_pos ~= nil then
						if vector.Distance(backup_pos, furthest_pos) > 0.2 then
							local furtherThanCurrent = vector.Distance(target_pos, furthest_pos) > vector.Distance(target_pos, backup_pos) + 1
							if target_dist > flee_distance or furtherThanCurrent then
								backup_pos = furthest_pos
							end
						end
					end
					back_pos_update_timer = math.random(1, 1.25)
				end
			else
				back_pos_update_timer = math.max(0, back_pos_update_timer - time.FrameDeltaTime())
			end

			--maintain proper distance to player
			if target_dist > attack.range + 1 then
				movement.SetDestination(target_pos, 0.5)
			elseif backup_pos ~= nil and vector.Distance(backup_pos, target_pos) > flee_distance then
				movement.SetDestination(backup_pos, 0.1)
				if movement.DestinationReached() then
					movement.RotateTowards(target_pos)
				end
			else
				if flee_pos == nil or (vector.Distance(flee_pos, target_pos) < flee_distance and movement.DestinationReached()) then
					flee_pos = movement.FindPositionAtDistance(target_pos, attack.range)
				end
				movement.SetDestination(flee_pos, 0.5)
			end

			--quit combat if we lose the player
			if movement.DestinationReached() and target_dist > attack.range + 1 and blockedLineOfSight then crawler.SetState(crawler.States.ROAMING) end

			--attack if the player is in line of sight and not too close
			if ranged_attack_cooldown == 0 and not blockedLineOfSight and targets.PlayerVisible() and vector.Distance(crawler.Position(), player.Position()) > attack.min_range then
				new_action(ProjectileAttack:new{parameters = attack, target = target})
				setAttackCooldown(attack.cooldown_range)
				startedAnAttack = true
			end
		end
	end

	return startedAnAttack
end

function sweepPlayerLineOfSight (crawler_pos, target_pos, min, max)
	local furthest_pos = nil
	local furthest_dist = -1
	local step = 5
	local angle = math.floor(vector.VectorToAngle(vector.Direction(target_pos, crawler_pos)) / step + 0.5) * step
	for a = angle - 60, angle + 60, step do
		local raycast_pos = collision.ThickRaycastDirection(target_pos, 0.5, vector.AngleToVector(a), max)
		local raycast_dist = vector.Distance(target_pos, raycast_pos)
		if raycast_dist > furthest_dist and raycast_dist > min then
			furthest_pos = raycast_pos
			furthest_dist = raycast_dist
		end
	end
	return furthest_pos
end
