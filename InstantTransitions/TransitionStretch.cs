using UnityEngine;

namespace InstantTransitions;

// Stretches the collider for the gate a little into the room to start loading earlier
public class TransitionStretch : MonoBehaviour
{
    // Velocity to add to real velocity to anticipate sudden acceleration such as Crystal Heart releasing
    // And we can use one value for both axes because dot product :)
    // private readonly Vector2 VELOCITY_ANTICIPATE = new(HeroController.instance.RUN_SPEED, HeroController.instance.JUMP_SPEED);
    private readonly Vector2 VELOCITY_ANTICIPATE = Vector2.zero;

    private TransitionPoint? tp;
    private BoxCollider2D? bc;

    private Vector3 originalOffset;
    private Vector2 originalSize;

    public void Awake()
    {
        tp = GetComponent<TransitionPoint>();
        bc = GetComponent<BoxCollider2D>();

        originalOffset = bc.offset;
        originalSize = bc.size;
    }

    public void Update()
    {
        Vector2 speed = HeroController.instance.current_velocity;
        float expectedLoadTime = 1f;

        Vector3 offset;
        Vector2 size;

        switch (tp!.GetGatePosition())
        {
            /*
             * For gates on the left, the left side of the gate should stay in the same spot,
             * and we should add speed * expectedLoadTime to the width.
             * (Although it should actually be subtraction because speed is negative)
             * 
             * originalOffset - originalSize.x / 2 = offset - size.x / 2
             * size.x = originalSize.x + speed.x * expectedLoadTime
             * 
             * offset = originalOffset + (size.x - originalSize.x) / 2
             * = originalOffset + speed.x * expectedLoadTime / 2
             * 
             * All other directions follow in a similar fashion.
             */

            case GlobalEnums.GatePosition.left:
                speed += Vector2.left * VELOCITY_ANTICIPATE;

                size = originalSize + Vector2.right * speed * expectedLoadTime;
                offset = originalOffset + Vector3.left * speed.x * expectedLoadTime / 2;

                break;

            case GlobalEnums.GatePosition.right:
                speed += Vector2.right * VELOCITY_ANTICIPATE;

                size = originalSize + Vector2.right * speed * expectedLoadTime;
                offset = originalOffset + Vector3.left * speed.x * expectedLoadTime / 2;

                break;

            case GlobalEnums.GatePosition.top:
                speed += Vector2.up * VELOCITY_ANTICIPATE;

                size = originalSize + Vector2.up * speed * expectedLoadTime;
                offset = originalOffset + Vector3.down * speed.y * expectedLoadTime / 2;

                break;

            case GlobalEnums.GatePosition.bottom:
                speed += Vector2.down * VELOCITY_ANTICIPATE;

                size = originalSize + Vector2.up * speed * expectedLoadTime;
                offset = originalOffset + Vector3.down * speed.y * expectedLoadTime / 2;

                break;

            default:
                return;
        }

        if (size.x < originalSize.x || size.y < originalSize.y)
        {
            size = originalSize;
            offset = originalOffset;
        }

        bc!.offset = offset;
        bc!.size = size;
    }
}
