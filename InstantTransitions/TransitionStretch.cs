using UnityEngine;

namespace InstantTransitions;

// Stretches the collider for the gate a little into the room to start loading earlier
public class TransitionStretch : MonoBehaviour
{
    private TransitionPoint? tp;
    private BoxCollider2D? bc;

    private Vector3 originalOffset;
    private Vector2 originalSize;

    private Vector2 lastVelocity;

    public void Awake()
    {
        tp = GetComponent<TransitionPoint>();
        bc = GetComponent<BoxCollider2D>();

        originalOffset = bc.offset;
        originalSize = bc.size;
    }

    public void FixedUpdate()
    {
        Vector2 velocity = HeroController.instance.current_velocity;
        Vector2 acceleration = velocity - lastVelocity;
        lastVelocity = velocity;

        float time = LoadTimePredictions.Predict(tp!.targetScene);

        Vector3 offset = default;
        Vector2 size = default;

        switch (tp!.GetGatePosition())
        {
            /*
             * For gates on the left, the left side of the gate should stay in the same spot,
             * and we should add velocity * time to the width.
             * (Remember velocity is negative for left and down transitions)
             * 
             * originalOffset - originalSize.x / 2 = offset - size.x / 2
             * size.x = originalSize.x + velocity.x * time
             * 
             * offset = originalOffset + (size.x - originalSize.x) / 2
             * 
             * For vertical transitions, we also want to factor in the acceleration of the player:
             * xf = x0 + v0t + 1/2*at^2
             * size.y = originalSize.y + velocity.y * time + acceleration.y * time * time
             */

            case GlobalEnums.GatePosition.left:
                if (velocity.x < 0)
                {
                    size = originalSize + Vector2.left * velocity.x * time;
                    offset = originalOffset + Vector3.right * (size.x - originalSize.x) / 2;
                }
                break;

            case GlobalEnums.GatePosition.right:
                if (velocity.x > 0)
                {
                    size = originalSize + Vector2.right * velocity.x * time;
                    offset = originalOffset + Vector3.left * (size.x - originalSize.x) / 2;
                }
                break;

            case GlobalEnums.GatePosition.top:
                if (velocity.y > 0)
                {
                    size = originalSize + Vector2.up * (velocity.y * time + acceleration.y * time * time / 2);
                    offset = originalOffset + Vector3.down * (size.y - originalSize.y) / 2;
                }
                break;

            case GlobalEnums.GatePosition.bottom:
                if (velocity.y < 0)
                {
                    size = originalSize + Vector2.down * (velocity.y * time + acceleration.y * time * time / 2);
                    offset = originalOffset + Vector3.up * (size.y - originalSize.y) / 2;
                }
                break;

            default:
                return;
        }

        if (size == default)
        {
            size = originalSize;
            offset = originalOffset;
        }

        bc!.offset = offset;
        bc!.size = size;
    }
}
