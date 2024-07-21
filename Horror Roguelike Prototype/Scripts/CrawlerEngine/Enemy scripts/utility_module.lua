
scripted_ragdoll_mode = nil

--called internally for player attacks
function take_damage(dmg, show_dmg, play_anim, force)
	if health > 0 then
		health = health - dmg
		if crawler.GetState() == crawler.States.ABSTRACTED then
			on_death ()
			crawler.Kill()
		else
			if show_dmg then
				render.CombatText("" .. dmg, vector.Add(crawler.Position(), vector.Up(0.5)), 1)
			end
			if health > 0 then
				if play_anim then
					anim.PlayStomped()
				end
				anim.PlayDamageEffect()
			else
				clear_actions()
				on_death ()
				crawler.Kill()
				update_ragdoll_mode()
				if force ~= nil then
					anim.ApplyForceToRagdoll(force)
				end
			end
		end
	end
	return health <= 0
end

function drawHealthBar()
	if health > 0 and health < standard_data.health and targets.PlayerVisible() then
		local healthBarLength = 20
		local pos = vector.Add(render.WorldToScreenPos(render.UIPosition()), {-healthBarLength / 2, 0, 0})
		local color
		if crawler.Is(StatusFX.POISONED) then
			color = {0, 1, 0, 0.5}
		else
			color = {1, 1, 1, 0.5}
		end
		render.DrawUILine(pos, vector.Add(pos, {healthBarLength, 0, 0}), {1, 0, 0, 0.5}, render.Coords.SCREEN)
		render.DrawUILine(pos, vector.Add(pos, {healthBarLength * (health / standard_data.health), 0, 0}), color, render.Coords.SCREEN)
	end
end

function update_ragdoll_mode()
	if health <= 0 then
		anim.SetRagdollMode(anim.RagdollModes.FULL)
		return
	end
	if scripted_ragdoll_mode ~= nil then
		anim.SetRagdollMode(scripted_ragdoll_mode)
		return
	end
	if crawler.Is(StatusFX.STUNNED) then
		anim.SetRagdollMode(anim.RagdollModes.FULL)
		return
	end
	anim.SetRagdollMode(anim.RagdollModes.NONE)
end

function set_scripted_ragdoll_mode(mode)
	scripted_ragdoll_mode = mode
	update_ragdoll_mode()
end
function reset_scripted_ragdoll_mode()
	scripted_ragdoll_mode = nil
	update_ragdoll_mode()
end
