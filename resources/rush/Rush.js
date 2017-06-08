let spawned = false

API.onResourceStart.connect(() => {
  API.setHudVisible(true)
})

API.onServerEventTrigger.connect((name, args) => {
  switch (name) {
    case 'spawned':
      spawned = args[0]
      break
    }
})

API.onUpdate.connect(() => {
  // API.setHudVisible(true)
  API.disableControlThisFrame(0)
})
