
action_queue = {}

function new_action (action)

	table.insert(action_queue, action)

end

function update_actions ()

	if #action_queue > 0 then

		--take the first in line
		local action = action_queue[1]

		--init
		if not action.initialized then
			action:init()
			action.initialized = true
		end

		--update
		if not action.finished then
			action:update()
		end

		--finish
		if action.finished then
			table.remove(action_queue, 1)
			action_queue = CleanNils(action_queue)
		end
	end
end

function clear_actions ()
	for i=#action_queue,1,-1 do
		cancel_action(i)
	end
	action_queue = {}
end
function remove_action (i)
	cancel_action(i)
	table.remove(action_queue, i)
	action_queue = CleanNils(action_queue)
end
function cancel_action (i)
	local action = action_queue[i]
	if action.initialized and not action.functionally_completed then
		anim.Stop()
		action:on_cancel()
	end
end

function targeting (target)
	for i=1,#action_queue do
		if action_queue[i].target == target then
			return true
		end
	end
	return false
end
function targeted ()
	local target_list = targets.TargetList()
	local crawler = crawler.AsTarget()
	for i=1,#target_list do
		if targets.Type(target_list[i]) == targets.Types.CRAWLER and targets.Call(target_list[i], "targeting", crawler) then
			return true
		end
	end
	return false
end

--internally called
function action_in_progress ()
	return #action_queue > 0 and not action_queue[1].functionally_completed
end
function action_allows_movement ()
	return #action_queue == 0 or action_queue[1].allow_movement or action_queue[1].allow_AI_movement
end

--internally called animation events
function action_event (event_parameters)
	if #action_queue > 0 then
		action_queue[1]:anim_event(event_parameters)
	end
end
function play_sound (event_parameters)
	float_param = event_parameters[1]
	int_param = event_parameters[2]
	string_param = event_parameters[3]
	sfx.Play(string_param, int_param, float_param)
end
function general_event (event_parameters)

end

--internally called when a target is deleted, stops all actions with that target
function on_target_deleted (target)
	for i=#action_queue,1,-1 do
		local action = action_queue[i]
		if action.target == target then
			action.target = nil
			remove_action(i)
		end
	end
end

--used for cleaning up nils from tables after they've had items removed
function CleanNils(t)
	local ans = {}
		for _,v in pairs(t) do
			ans[ #ans+1 ] = v
		end
	return ans
end
