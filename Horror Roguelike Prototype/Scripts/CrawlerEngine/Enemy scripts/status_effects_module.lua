
--the default status effects are internally used, so don't change them
--you can, however, add new status effects to the list
StatusFX = {
   POISONED = 1,
   STUNNED = 2,
   CONFUSED = 3,
   BLINDED = 4,
   ENRAGED = 5,
   SLOWED = 6,
   SHACKLED = 7,
}

--internally called to handle initialization
function init_status_effect(effect)
	if effect == StatusFX.STUNNED then
		update_ragdoll_mode()
	end
end

--handle status effect expiration
function end_status_effect(effect)
	if effect == StatusFX.STUNNED then
		update_ragdoll_mode()
	end
end

--status effects are decremented here and any recurring effects are applied at intervals
function update_status_effects(delta_time)

	local status_effects = crawler.GetStatusFX()
	for i=1,#status_effects do
		local effect = status_effects[i][1]
		local time = status_effects[i][2]

		if effect == StatusFX.POISONED then
			local passed_intervals = get_passed_intervals(time, delta_time, 1)
			if passed_intervals > 0 then
				local dmg = 2
				take_damage(dmg * passed_intervals, true, false, nil)
			end
		end

		crawler.SetStatusFXTime(i, time - delta_time)

		if time - delta_time <= 0 then
			end_status_effect(effect)
		end
	end
end

function get_passed_intervals(time, delta_time, interval)
	return math.floor(delta_time / interval) + (time % interval <= delta_time % interval and 1 or 0)
end