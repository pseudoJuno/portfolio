
roam_pause_timer = 0

function update_roaming ()

	local destination_outside_anchor_radius = vector.Distance(movement.GetDestination(), crawler.RoamAnchor()) > 3

	if movement.DestinationReached() or destination_outside_anchor_radius then
		roam_pause_timer = math.max(0, roam_pause_timer - time.FrameDeltaTime())
		if roam_pause_timer == 0 or destination_outside_anchor_radius then
			local roam_pos = nil
			for i=1,10 do
				roam_pos = vector.Add(crawler.RoamAnchor(), vector.RndPosInRadius(2.5))
				if not collision.LinecastObstacles(crawler.RoamAnchor(), roam_pos, 0.5) then
					movement.SetDestination(roam_pos, movement.CharacterRadius() + 0.5)
					roam_pause_timer = math.random() * 5
					break
				end
			end
		end
	end

	anim.LookForward();
end

--called internally if a crawler needs to aggro when spawned
function aggro(leap)
	if leap ~= nil and player.SeenFromBehindCorner() then
		new_action(Leap:new{parameters = leap, anim_side = nil, direction = player.CornerTurnDirection(), distance = 1.5, speed = 0.7})
		sfx.Play(sfx.Sounds.AGGRO, 0, 1)
		crawler.SetState(crawler.States.COMBAT)
	else
		new_action(Aggro:new{})
	end
end