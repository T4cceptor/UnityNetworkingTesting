using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using VRTK;

namespace Innoactive.Toolkit.Multiuser
{
    /// <summary>
    /// networking component enabling networking for any
    /// interactable object
    /// </summary>
    [RequireComponent(typeof(PhotonView), typeof(VRTK_InteractableObject))]
    [AddComponentMenu("Innoactive/Multiuser/Networking/[Networking] Interactable Object")]
    public class InteractableObjectNetworking : CorrectivePhysicsNetworking
    {
        #region Inspector Variables
        /// <summary>
        /// whether or not the touch commands (start and stop touch) should be synchronized
        /// </summary>
        [Tooltip("Whether or not to synchronize touches via the network")]
        [SerializeField]
        private bool synchronizeTouch = true;

        /// <summary>
        /// whether or not the grabbing commands (start and stop grab) should be synchronized
        /// </summary>
        [Tooltip("Whether or not to synchronize grabs via the network")]
        [SerializeField]
        private bool synchronizeGrab = true;

        /// <summary>
        /// whether or not the usage commands (start and stop using) should be synchronized
        /// </summary>
        [Tooltip("Whether or not to synchronize usage via the network")]
        [SerializeField]
        private bool synchronizeUsage = true;
        #endregion

        #region private members
        /// <summary>
        /// reference to the (non-networked) stroke
        /// </summary>
        protected VRTK_InteractableObject interactableObject;

        /// <summary>
        /// backup variable for the kinematic state of the object
        /// </summary>
        private bool originalIsKinematic = false;

        /// <summary>
        /// the rigidbody attached to our interactable object
        /// required for physics properties
        /// </summary>
        protected Rigidbody riggy;
        #endregion

        #region Unity Magic Methods
        protected override void Awake()
        {
            base.Awake();
            // get references to the actual object
            interactableObject = GetComponent<VRTK_InteractableObject>();
            // get a reference to the rigidbody of the object
            riggy = interactableObject.GetComponent<Rigidbody>();
            // setup transform synchronization
            SetupTransformView();
        }

        protected virtual void Start()
        {
            // register for touch and untouch events to "own" the interactable object as a user
            interactableObject.InteractableObjectTouched += OnInteractableObjectTouched;
            interactableObject.InteractableObjectUntouched += OnInteractableObjectUntouched;
            // register for grab and ungrab events to toggle the controller visibility on other clients
            interactableObject.InteractableObjectGrabbed += OnInteractableObjectGrabbed;
            interactableObject.InteractableObjectUngrabbed += OnInteractableObjectUngrabbed;
            // register for use and stop use events to sync usage to other clients
            interactableObject.InteractableObjectUsed += OnInteractableObjectUsed;
            interactableObject.InteractableObjectUnused += OnInteractableObjectUnused;
        }

        protected virtual void OnDestroy()
        {
            // unregister from touch and untouch events to "own" the interactable object as a user
            interactableObject.InteractableObjectTouched -= OnInteractableObjectTouched;
            interactableObject.InteractableObjectUntouched -= OnInteractableObjectUntouched;
            // unregister from grab and ungrab events to toggle the controller visibility on other clients
            interactableObject.InteractableObjectGrabbed -= OnInteractableObjectGrabbed;
            interactableObject.InteractableObjectUngrabbed -= OnInteractableObjectUngrabbed;
            // unregister for use and stop use events to no longer sync usage to other clients
            interactableObject.InteractableObjectUsed -= OnInteractableObjectUsed;
            interactableObject.InteractableObjectUnused -= OnInteractableObjectUnused;
        }

        /// <summary>
        /// on every physics update, update the velocity and angular velocity
        /// of the objects which will be synchronized to remote clients
        /// </summary>
        protected virtual void Update()
        {
            //// special case if physcis are applied to our object
            //if (riggy.useGravity == true)
            //{
            //    // if I am not in charge of the object, turn the object kinematic as someone else will calculate the physics for me
            //    if (photonView.isMine == false && riggy.isKinematic == false)
            //    {
            //        //riggy.isKinematic = true;
            //    }
            //    else if (photonView.isMine == true && riggy.isKinematic == true)
            //    {
            //        //riggy.isKinematic = false;
            //    }
            //}
            //// this function needs to be called in order to sync the correct amoungs for velocity and angular velocity
            //// see http://doc-api.photonengine.com/en/pun/current/class_photon_transform_view.html
            //SetSynchronizedValues(riggy.velocity, riggy.angularVelocity.magnitude);
            // base functionality
        }
        #endregion

        /// <summary>
        /// sets up the referenced photon transform view
        /// with interpolation and extrapolation options
        ///
        /// TODO: Allow changing these values through the editor
        /// </summary>
        protected virtual void SetupTransformView()
        {
            //// setup the transformview to track position and rotation
            //// synchronize
            //PositionModel.SynchronizeEnabled = true;
            //// interpolate
            //PositionModel.InterpolateOption = PhotonTransformViewPositionModel.InterpolateOptions.EstimatedSpeed;
            //// extrapolate
            //PositionModel.ExtrapolateOption = PhotonTransformViewPositionModel.ExtrapolateOptions.SynchronizeValues;
            //// rotation
            //// synchronize
            //RotationModel.SynchronizeEnabled = true;
            //// interpolate
            //RotationModel.InterpolateOption = PhotonTransformViewRotationModel.InterpolateOptions.RotateTowards;
            //// actually observe the transforms to update
            //if (photonView.ObservedComponents == null)
            //{
            //    photonView.ObservedComponents = new List<Component>();
            //}
            //photonView.ObservedComponents.Add(this);
            //// make sure to also synchronize scale
            //ScaleModel.SynchronizeEnabled = true;
            // turn on synchronization finally
            photonView.synchronization = ViewSynchronization.UnreliableOnChange;
            // let others take over
            photonView.ownershipTransfer = OwnershipOption.Takeover;
        }

        #region Event Handlers
        /// <summary>
        /// called when the interactable object is touched by the user
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnInteractableObjectTouched(object sender, InteractableObjectEventArgs e)
        {
            // get ownership of the pencil for the grabbing player
            if (photonView.isMine == false)
            {
                photonView.TransferOwnership(PhotonNetwork.player);
            }
            // notify networked interactable objects about the beginning touch
            if (synchronizeTouch)
            {
                photonView.RPC("HandleTouch", PhotonTargets.Others, PhotonNetwork.player);
            }
        }

        /// <summary>
        /// called when the interactable object is no longer touched by the user
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnInteractableObjectUntouched(object sender, InteractableObjectEventArgs e)
        {
            if (photonView.isMine && synchronizeTouch)
            {
                // notify networked interactable objects about the ended touch
                photonView.RPC("HandleUntouch", PhotonTargets.Others, PhotonNetwork.player);
            }
        }

        /// <summary>
        /// called whenever the interactable object is grabbed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnInteractableObjectGrabbed(object sender, InteractableObjectEventArgs e)
        {
            IsBeingControlled = true;
            if (photonView.isMine && synchronizeGrab)
            {
                // notify networked interactable objects about the grab
                photonView.RPC("HandleGrab", PhotonTargets.Others, PhotonNetwork.player);
            }
        }

        /// <summary>
        /// called whenever the interactable object is no longer grabbed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnInteractableObjectUngrabbed(object sender, InteractableObjectEventArgs e)
        {
            IsBeingControlled = false;
            if (photonView.isMine && synchronizeGrab)
            {
                // notify networked interactable objects about the ending grab
                photonView.RPC("HandleUngrab", PhotonTargets.Others, PhotonNetwork.player);
            }
        }

        /// <summary>
        /// called whenever the interactable object is used
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnInteractableObjectUsed(object sender, InteractableObjectEventArgs e)
        {
            if (photonView.isMine && synchronizeUsage)
            {
                // notify networked interactable objects about the usage
                photonView.RPC("HandleUse", PhotonTargets.Others, PhotonNetwork.player);
            }
        }

        /// <summary>
        /// called whenever the interactable object is no longer used
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnInteractableObjectUnused(object sender, InteractableObjectEventArgs e)
        {
            if (photonView.isMine && synchronizeUsage)
            {
                // notify networked interactable objects about the usage
                photonView.RPC("HandleUnuse", PhotonTargets.Others, PhotonNetwork.player);
            }
        }
        #endregion

        #region Photon Methods
        /// <summary>
        /// called when the interactable object is touched by a remote client
        /// </summary>
        [PunRPC]
        protected void HandleTouch(PhotonPlayer player)
        {
            // call the overridable handler
            OnTouched(player);
        }

        /// <summary>
        /// called when a remote client stopped touching
        /// </summary>
        [PunRPC]
        protected void HandleUntouch(PhotonPlayer player)
        {
            // call the overridable handler
            OnUntouched(player);
        }

        /// <summary>
        /// called when the interactable object is grabbed by a remote client
        /// </summary>
        [PunRPC]
        protected void HandleGrab(PhotonPlayer player)
        {
            // call the overridable handler
            OnGrabbed(player);
        }

        /// <summary>
        /// called when the interactable object is no longger grabbed by a remote client
        /// </summary>
        [PunRPC]
        protected void HandleUngrab(PhotonPlayer player)
        {
            // call the overridable handler
            OnUngrabbed(player);
        }

        /// <summary>
        /// called when the interactable object is touched by a remote client
        /// </summary>
        [PunRPC]
        protected void HandleUse(PhotonPlayer player)
        {
            // call the overridablehandler
            OnUsed(player);
        }

        /// <summary>
        /// called when a remote client stopped touching
        /// </summary>
        [PunRPC]
        protected void HandleUnuse(PhotonPlayer player)
        {
            // call the overridable handler
            OnUnused(player);
        }
        #endregion

        #region Overridable Touch, Use, Grab Handlers
        /// <summary>
        /// handler executed when an object is touched on a remote client
        /// </summary>
        protected virtual void OnTouched(PhotonPlayer player)
        {
            // turn on the highlighter
            interactableObject.ToggleHighlight(true);
        }

        /// <summary>
        /// handler executed when an object is no longer touched on a remote client
        /// </summary>
        protected virtual void OnUntouched(PhotonPlayer player)
        {
            // "untoggle" the highlighter
            interactableObject.ToggleHighlight(false);
        }

        /// <summary>
        /// handler executed when an object is grabbed on a remote client
        /// </summary>
        protected virtual void OnGrabbed(PhotonPlayer player)
        {
            // make sure that our rigidbody is not affected by physics while being dragged around
            var riggy = interactableObject.GetComponent<Rigidbody>();
            if (riggy == null) return;
            riggy.detectCollisions = false;
            // turn the rigid body kinematic so it is not affected by physics anymore
            originalIsKinematic = interactableObject.isKinematic;
            interactableObject.isKinematic = true;
            // remove touch highlighting as well
            interactableObject.ToggleHighlight(false);
        }

        /// <summary>
        /// handler executed when an object is no longer grabbed on a remote client
        /// </summary>
        protected virtual void OnUngrabbed(PhotonPlayer player)
        {
            // make sure that our rigidbody is affected by physics again
            var riggy = interactableObject.GetComponent<Rigidbody>();
            if (riggy == null) return;
            riggy.detectCollisions = true;
            interactableObject.isKinematic = originalIsKinematic;
        }

        /// <summary>
        /// handler executed when an object is used on a remote client
        /// </summary>
        protected virtual void OnUsed(PhotonPlayer player)
        {
            interactableObject.StartUsing(player.TagObject as GameObject);
        }

        /// <summary>
        /// handler executed when an object is no longer used on a remote client
        /// </summary>
        protected virtual void OnUnused(PhotonPlayer player)
        {
            interactableObject.StopUsing(player.TagObject as GameObject);
        }
        #endregion
    }
}
