let spawned = false
let softBoundsMessage = false
let softBoundsFade = 0
let screen = API.getScreenResolutionMantainRatio()
let UIKit
let uiElements = new Set()
let phase = 0
let alreadySetup = false

let anchors = {
  center: {
    x: screen.Width * 0.5,
    y: screen.Height * 0.5
  },

  topCenter: {
    x: screen.Width * 0.5,
    y: 50
  }
}

API.onResourceStart.connect(() => {
  API.setHudVisible(true)
  UIKit = resource.uikit.__requireModuleClasses()
})

API.onServerEventTrigger.connect((name, args) => {
  switch (name) {
    case 'uiSetup':
      uiSetup(args)
      break
    case 'newPhase':
      phase = args[0]
      break
    case 'spawned':
      spawned = args[0]
      break
    case 'softBounds:exit':
      softBoundsExit()
      break
    case 'softBounds:enter':
    case 'softBounds:died':
      softBoundsReset()
      break
  }
})

function uiSetup ([ objectives ]) {
  if (alreadySetup) {
    return
  }

  alreadySetup = true
  API.sendChatMessage(objectives)
  objectives = JSON.parse(objectives)

  const objectiveRect = new UIKit.Rect({
    fromCenter: true,
    x: anchors.topCenter.x,
    y: anchors.topCenter.y,
    h: 30,
    w: Math.round((33 * 3) + 10),
    opacity: 70,
    color: '#000'
  })
  uiElements = uiElements.add(objectiveRect)
  const team = API.getEntitySyncedData(API.getLocalPlayer(), 'team')

  objectives.forEach((v, k) => {
    uiElements = uiElements.add(
      objectiveRect.getInsetRect({
        x: (k * 35) + 5,
        y: 5,
        w: 30,
        h: 30
      })
      .border({
        color: '#0bf',
        width: 2
      })
      .text({ position: 'top center', scale: 0.3, content: 'A', color: '#fff', font: 2 })
      // .connect((rect) => {
      //   if (phase === k) {
      //     rect.border({
      //       color: '#08f',
      //       width: 2
      //     })
      //   }
      // })
      .renderBackground(false)
    )
  })
}

function softBoundsExit () {
  softBoundsMessage = true
  API.playScreenEffect('MinigameEndTrevor', 10000, true)
  API.playScreenEffect('DeathFailOut', 10000, true)
}

function softBoundsReset () {
  softBoundsMessage = false
  softBoundsFade = 0
  API.callNative('_STOP_ALL_SCREEN_EFFECTS')
}

API.onUpdate.connect(() => {
  try {
    // API.setHudVisible(true)
    API.disableControlThisFrame(0)

    API.displaySubtitle(`~q~UI Elements this frame:~w~ ${uiElements.size}`)
    if (uiElements.size !== 0) {
      uiElements.forEach(x => x.draw())
    }

    if (softBoundsMessage) {
      API.drawText(
        'Return to the fight',
        anchors.center.x,
        anchors.center.y,
        1,
        255,
        0,
        0,
        softBoundsFade++ % 255,
        2,
        1,
        false,
        true,
        100402350
      )
    }
  } catch (e) {
    //
  }
})
