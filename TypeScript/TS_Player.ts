// YourProject/TypeScript/TS_Player

import * as UE from 'ue'

class TS_Player extends UE.Character {
    Constructor() {
        //...
    }

    ReceiveBeginPlay(): void {
        //...
    }
    ReceiveTick(InDeltaSeconds: number): void {
        //...
        console.log("sssssssssssss");
    }
    //...
}

export default TS_Player;